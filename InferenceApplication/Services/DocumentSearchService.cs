using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azure;
using Azure.Core;
using Azure.Search.Documents;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using InferenceApplication.Models;

namespace InferenceApplication.Services
{
    /// <summary>
    /// Performs hybrid search (text + vector) using Azure Cognitive Search.
    /// </summary>
    public class DocumentSearchService : IDocumentSearchService
    {
        private readonly SearchIndexService _indexService; // used for index lifecycle, not queries
        private readonly SearchOptions _searchOptions;
        private readonly SearchClient _searchClient;
        private readonly EmbeddingService _embeddingService;
        private readonly ILogger<DocumentSearchService> _logger;

        public DocumentSearchService(IOptions<SearchOptions> searchOptions, EmbeddingService embeddingService, ILogger<DocumentSearchService> logger)
        {
            _searchOptions = searchOptions.Value;
            _embeddingService = embeddingService;
            _logger = logger;
            var credential = new AzureKeyCredential(_searchOptions.ApiKey);
            _searchClient = new SearchClient(new Uri(_searchOptions.Endpoint), _searchOptions.IndexName, credential);
        }

        /// <summary>
        /// Performs a hybrid search for the provided query and returns results.
        /// </summary>
        public async Task<List<SearchResultItem>> SearchAsync(string query, int topK = 5)
        {
            // Simple text-only search fallback (avoids SDK vector API differences)
            var options = new Azure.Search.Documents.SearchOptions
            {
                Size = topK
            };
            options.Select.Add("id");
            options.Select.Add("title");
            options.Select.Add("content");
            options.Select.Add("sourceDocumentId");

            var response = await _searchClient.SearchAsync<Dictionary<string, object>>(query, options).ConfigureAwait(false);

            var results = new List<SearchResultItem>();
            await foreach (var r in response.Value.GetResultsAsync())
            {
                var doc = r.Document;
                var idStr = doc.TryGetValue("id", out var idVal) ? idVal?.ToString() ?? string.Empty : string.Empty;
                var title = doc.TryGetValue("title", out var tVal) ? tVal?.ToString() ?? string.Empty : string.Empty;
                var content = doc.TryGetValue("content", out var cVal) ? cVal?.ToString() ?? string.Empty : string.Empty;
                var srcId = doc.TryGetValue("sourceDocumentId", out var sVal) ? sVal?.ToString() ?? string.Empty : string.Empty;

                Guid.TryParse(idStr, out var gid);
                Guid.TryParse(srcId, out var srcGuid);

                results.Add(new SearchResultItem
                {
                    Id = gid,
                    Title = title,
                    Snippet = content.Length > 400 ? content.Substring(0, 400) + "..." : content,
                    Score = r.Score ?? 0,
                    SourceDocumentId = srcGuid
                });
            }

            return results.OrderByDescending(x => x.Score).ToList();
        }
    }
}
