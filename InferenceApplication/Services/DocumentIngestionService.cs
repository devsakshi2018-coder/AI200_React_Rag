using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using InferenceApplication.Models;
using InferenceApplication.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InferenceApplication.Services
{
    /// <summary>
    /// Options for ingestion (chunk size heuristics).
    /// </summary>
    public class IngestionOptions
    {
        public int ChunkMinTokens { get; set; } = 400;
        public int ChunkMaxTokens { get; set; } = 800;
        public int OverlapTokens { get; set; } = 50;
    }

    /// <summary>
    /// Orchestrates document ingestion: extract -> chunk -> embed -> persist
    /// </summary>
    public class InferenceApplicationService : IInferenceApplicationService
    {
        private readonly EmbeddingService _embeddingService;
        private readonly DocumentRepository _repository;
        private readonly SearchIndexService _indexService;
        private readonly IngestionOptions _options;
        private readonly ILogger<InferenceApplicationService> _logger;

        private readonly TextChunker _chunker;

        public InferenceApplicationService(
            EmbeddingService embeddingService,
            DocumentRepository repository,
            SearchIndexService indexService,
            IOptions<IngestionOptions> options,
            ILogger<InferenceApplicationService> logger,
            TextChunker chunker)
        {
            _embeddingService = embeddingService;
            _repository = repository;
            _indexService = indexService;
            _options = options.Value;
            _logger = logger;
            _chunker = chunker;
        }

        /// <summary>
        /// Ingests a file stream with a title and optional metadata. Supports .txt, .pdf, .docx.
        /// </summary>
        public async Task<Guid> IngestAsync(Stream fileStream, string fileName, string title, Dictionary<string, object>? metadata = null, CancellationToken cancellationToken = default)
        {
            // 1. Extract text
            var text = await ExtractTextAsync(fileStream, fileName).ConfigureAwait(false);

            // 2. Chunk text
            var chunkStrings = _chunker.ChunkText(text, _options.ChunkMaxTokens, _options.OverlapTokens);
            var chunks = chunkStrings.Select(s => new { Text = s }).ToList();

            // 3. Generate embeddings
            var chunkContents = chunks.Select(c => c.Text).ToList();
            // Fallback to per-chunk embedding calls to avoid relying on a batched helper
            var embeddings = new List<float[]>();
            foreach (var chunkText in chunkContents)
            {
                var emb = await _embeddingService.GetEmbeddingAsync(chunkText).ConfigureAwait(false);
                embeddings.Add(emb);
            }

            // 4. Prepare documents and persist to both Postgres and Azure Search
            var sourceDocumentId = Guid.NewGuid();
            var docs = new List<Document>();

            for (int i = 0; i < chunks.Count; i++)
            {
                var d = new Document
                {
                    Id = Guid.NewGuid(),
                    Title = title ?? fileName,
                    Content = chunks[i].Text,
                    ContentVector = embeddings.ElementAtOrDefault(i),
                    ChunkIndex = i,
                    SourceDocumentId = sourceDocumentId,
                    Metadata = metadata == null ? null : System.Text.Json.Nodes.JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(metadata))?.AsObject(),
                    CreatedAt = DateTime.UtcNow
                };

                // Save to Postgres
                await _repository.SaveDocumentAsync(d).ConfigureAwait(false);
                docs.Add(d);
            }

            // Index into Azure Search in batches
            await _indexService.UploadDocumentsAsync(docs).ConfigureAwait(false);

            return sourceDocumentId;
        }

        private async Task<string> ExtractTextAsync(Stream stream, string fileName)
        {
            var ext = Path.GetExtension(fileName).ToLowerInvariant();
            switch (ext)
            {
                case ".txt":
                {
                    using var reader = new StreamReader(stream, Encoding.UTF8, true, 8192, leaveOpen: true);
                    stream.Position = 0;
                    return await reader.ReadToEndAsync().ConfigureAwait(false);
                }
                case ".pdf":
                    return await ExtractTextFromPdfAsync(stream).ConfigureAwait(false);
                case ".docx":
                    return await ExtractTextFromDocxAsync(stream).ConfigureAwait(false);
                default:
                    throw new NotSupportedException($"Unsupported file extension: {ext}");
            }
        }

        private async Task<string> ExtractTextFromPdfAsync(Stream stream)
        {
            try
            {
                // Use PdfPig for PDF text extraction
                stream.Position = 0;
                using var doc = UglyToad.PdfPig.PdfDocument.Open(stream);
                var sb = new StringBuilder();
                foreach (var page in doc.GetPages())
                {
                    sb.AppendLine(page.Text);
                }
                return sb.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to extract PDF text");
                return string.Empty;
            }
        }

        private async Task<string> ExtractTextFromDocxAsync(Stream stream)
        {
            try
            {
                stream.Position = 0;
                using var mem = new MemoryStream();
                await stream.CopyToAsync(mem).ConfigureAwait(false);
                mem.Position = 0;

                using var word = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(mem, false);
                var body = word.MainDocumentPart?.Document?.Body;
                return body?.InnerText ?? string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to extract DOCX text");
                return string.Empty;
            }
        }

        private record Chunk(string Text);

        private List<Chunk> ChunkText(string text, int maxTokens, int overlapTokens)
        {
            if (string.IsNullOrWhiteSpace(text)) return new List<Chunk> { new Chunk(string.Empty) };

            // Very simple approximate tokenization: assume ~4 characters per token
            var approxTokensPerChar = 1.0 / 4.0;
            var maxChars = (int)(maxTokens / approxTokensPerChar);
            var overlapChars = (int)(overlapTokens / approxTokensPerChar);

            var chunks = new List<Chunk>();

            int pos = 0;
            while (pos < text.Length)
            {
                var length = Math.Min(maxChars, text.Length - pos);
                var segment = text.Substring(pos, length);

                // Try to break at sentence boundary for readability
                var lastPeriod = segment.LastIndexOfAny(new[] { '.', '\n' });
                if (lastPeriod > 0 && length == maxChars)
                {
                    segment = segment.Substring(0, lastPeriod + 1);
                }

                chunks.Add(new Chunk(segment.Trim()));

                pos += segment.Length - overlapChars;
                if (pos < 0) pos = 0;
            }

            return chunks;
        }
    }
}
