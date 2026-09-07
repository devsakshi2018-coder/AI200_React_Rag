using System;
using System.Text.Json.Nodes;

namespace InferenceApplication.Models
{
    /// <summary>
    /// Represents a document chunk stored in the system, including vector embedding metadata.
    /// </summary>
    public class Document
    {
        /// <summary>
        /// Chunk-level unique identifier.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// The title of the source document.
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// The textual content of this chunk.
        /// </summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// The vector embedding stored as a float array.
        /// </summary>
        public float[]? ContentVector { get; set; }

        /// <summary>
        /// Index of the chunk within the source document (0-based).
        /// </summary>
        public int ChunkIndex { get; set; }

        /// <summary>
        /// The original source document id (per upload).
        /// </summary>
        public Guid SourceDocumentId { get; set; }

        /// <summary>
        /// Optional JSON metadata about the document (author, path, etc.).
        /// </summary>
        public JsonObject? Metadata { get; set; }

        /// <summary>
        /// Creation timestamp in UTC.
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Request DTO for search API.
    /// </summary>
    public class SearchRequest
    {
        /// <summary>
        /// Natural language query.
        /// </summary>
        public string Query { get; set; } = string.Empty;

        /// <summary>
        /// Number of top results to return.
        /// </summary>
        public int TopK { get; set; } = 5;
    }

    /// <summary>
    /// Search result DTO returned from the search endpoint.
    /// </summary>
    public class SearchResultItem
    {
        /// <summary>
        /// Chunk id.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Title of the source document.
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Matched content snippet.
        /// </summary>
        public string Snippet { get; set; } = string.Empty;

        /// <summary>
        /// Relevance score returned by the search provider.
        /// </summary>
        public double Score { get; set; }

        /// <summary>
        /// Reference to the source document upload id.
        /// </summary>
        public Guid SourceDocumentId { get; set; }
    }
}


public class DocumentUploadRequest
{
    public IFormFile File { get; set; }
    public string? Title { get; set; }
}