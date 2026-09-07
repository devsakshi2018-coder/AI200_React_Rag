using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Npgsql;
using NpgsqlTypes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pgvector;
using InferenceApplication.Models;

namespace InferenceApplication.Repositories
{
    /// <summary>
    /// Options for PostgreSQL connection.
    /// </summary>
    public class PostgresOptions
    {
        public string ConnectionString { get; set; } = string.Empty;
    }

    /// <summary>
    /// Repository for storing and querying document chunks using pgvector.
    /// </summary>
    public class DocumentRepository
    {
        private readonly PostgresOptions _options;
        private readonly ILogger<DocumentRepository> _logger;

        /// <summary>
        /// Constructs a new instance of DocumentRepository.
        /// </summary>
        public DocumentRepository(IOptions<PostgresOptions> options, ILogger<DocumentRepository> logger)
        {
            _options = options.Value;
            _logger = logger;
            // Ensure vector type mapping available
            NpgsqlConnection.GlobalTypeMapper.UseVector();
        }

        /// <summary>
        /// Saves a document chunk into the database.
        /// </summary>
        public async Task SaveDocumentAsync(Document doc)
        {
            const string sql = @"
INSERT INTO documents (id, title, content, content_vector, chunk_index, source_document_id, metadata, created_at)
VALUES (@id, @title, @content, @vector, @chunk_index, @source_id, @metadata, @created_at)
ON CONFLICT (id) DO UPDATE
SET title = EXCLUDED.title,
    content = EXCLUDED.content,
    content_vector = EXCLUDED.content_vector,
    chunk_index = EXCLUDED.chunk_index,
    source_document_id = EXCLUDED.source_document_id,
    metadata = EXCLUDED.metadata,
    created_at = EXCLUDED.created_at;";

            await using var conn = new NpgsqlConnection(_options.ConnectionString);
            await conn.OpenAsync().ConfigureAwait(false);
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("id", doc.Id);
            cmd.Parameters.AddWithValue("title", doc.Title);
            cmd.Parameters.AddWithValue("content", doc.Content);
            cmd.Parameters.Add(new NpgsqlParameter("vector", NpgsqlDbType.Array | NpgsqlDbType.Real)
            {
                Value = (object?)doc.ContentVector ?? DBNull.Value
            });
            cmd.Parameters.AddWithValue("chunk_index", doc.ChunkIndex);
            cmd.Parameters.AddWithValue("source_id", doc.SourceDocumentId);
            cmd.Parameters.AddWithValue("metadata", (object?)(doc.Metadata?.ToJsonString() ?? JsonDocument.Parse("{}").RootElement.ToString()));
            cmd.Parameters.AddWithValue("created_at", doc.CreatedAt);
            await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Searches for similar documents using cosine distance operator provided by pgvector.
        /// Returns top K closest documents.
        /// </summary>
        public async Task<List<Document>> SearchSimilarAsync(float[] queryVector, int topK = 5)
        {
            const string sql = @"
SELECT id, title, content, content_vector, chunk_index, source_document_id, metadata, created_at,
       content_vector <=> @q AS distance
FROM documents
ORDER BY content_vector <=> @q
LIMIT @k;";

            var results = new List<Document>();

            await using var conn = new NpgsqlConnection(_options.ConnectionString);
            await conn.OpenAsync().ConfigureAwait(false);
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.Add(new NpgsqlParameter("q", NpgsqlDbType.Array | NpgsqlDbType.Real) { Value = queryVector });
            cmd.Parameters.AddWithValue("k", topK);

            await using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                var doc = new Document
                {
                    Id = reader.GetGuid(0),
                    Title = reader.GetString(1),
                    Content = reader.GetString(2),
                    ContentVector = reader.IsDBNull(3) ? null : reader.GetFieldValue<float[]>(3),
                    ChunkIndex = reader.GetInt32(4),
                    SourceDocumentId = reader.GetGuid(5),
                    Metadata = reader.IsDBNull(6) ? null : JsonNode.Parse(reader.GetString(6))?.AsObject(),
                    CreatedAt = reader.GetDateTime(7)
                };

                results.Add(doc);
            }

            return results;
        }

        /// <summary>
        /// Gets a document chunk by id.
        /// </summary>
        public async Task<Document?> GetDocumentByIdAsync(Guid id)
        {
            const string sql = @"SELECT id, title, content, content_vector, chunk_index, source_document_id, metadata, created_at FROM documents WHERE id = @id";

            await using var conn = new NpgsqlConnection(_options.ConnectionString);
            await conn.OpenAsync().ConfigureAwait(false);
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("id", id);

            await using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
            if (await reader.ReadAsync().ConfigureAwait(false))
            {
                return new Document
                {
                    Id = reader.GetGuid(0),
                    Title = reader.GetString(1),
                    Content = reader.GetString(2),
                    ContentVector = reader.IsDBNull(3) ? null : reader.GetFieldValue<float[]>(3),
                    ChunkIndex = reader.GetInt32(4),
                    SourceDocumentId = reader.GetGuid(5),
                    Metadata = reader.IsDBNull(6) ? null : JsonNode.Parse(reader.GetString(6))?.AsObject(),
                    CreatedAt = reader.GetDateTime(7)
                };
            }

            return null;
        }

        /// <summary>
        /// Deletes a document chunk by id.
        /// </summary>
        public async Task DeleteDocumentAsync(Guid id)
        {
            const string sql = @"DELETE FROM documents WHERE id = @id";
            await using var conn = new NpgsqlConnection(_options.ConnectionString);
            await conn.OpenAsync().ConfigureAwait(false);
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("id", id);
            await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
        }
    }
}
