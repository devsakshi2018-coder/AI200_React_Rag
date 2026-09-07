using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using InferenceApplication.Models;

namespace InferenceApplication.Services
{
    /// <summary>
    /// Options for Azure Cognitive Search.
    /// </summary>
    public class SearchOptions
    {
        public string Endpoint { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
        public string IndexName { get; set; } = "documents-index";
        public int VectorDimension { get; set; } = 1536;
    }

    /// <summary>
    /// Service responsible for index creation and uploading documents to Azure Cognitive Search.
    /// </summary>
    public class SearchIndexService
    {
        private readonly SearchOptions _options;
        private readonly SearchIndexClient _indexClient;
        private readonly SearchClient _searchClient;
        private readonly ILogger<SearchIndexService> _logger;

        public SearchIndexService(IOptions<SearchOptions> options, ILogger<SearchIndexService> logger)
        {
            _options = options.Value;
            if (!string.IsNullOrWhiteSpace(_options.ApiKey))
            {
                var credential = new AzureKeyCredential(_options.ApiKey);
                _indexClient = new SearchIndexClient(new Uri(_options.Endpoint), credential);
                _searchClient = new SearchClient(new Uri(_options.Endpoint), _options.IndexName, credential);
            }
            else
            {
                var credential = new DefaultAzureCredential();
                _indexClient = new SearchIndexClient(new Uri(_options.Endpoint), credential);
                _searchClient = new SearchClient(new Uri(_options.Endpoint), _options.IndexName, credential);
            }
            _logger = logger;
        }

        /// <summary>
        /// Ensures the search index exists with a vector field.
        /// This method is idempotent and safe to call at startup.
        /// </summary>
        public async Task EnsureIndexExistsAsync()
        {
            try
            {
                var fieldBuilder = new FieldBuilder();
                var searchFields = new List<SearchField>
                {
                    new SimpleField("id", SearchFieldDataType.String) { IsKey = true, IsFilterable = true },
                    new SearchField("title", SearchFieldDataType.String) { IsSearchable = true, IsFilterable = true },
                    new SearchField("content", SearchFieldDataType.String) { IsSearchable = true, AnalyzerName = LexicalAnalyzerName.EnMicrosoft },
                    new SearchField("sourceDocumentId", SearchFieldDataType.String) { IsFilterable = true, IsFacetable = false },
                    new SearchField("chunkIndex", SearchFieldDataType.Int32) { IsFilterable = true },
                    new SearchField("createdAt", SearchFieldDataType.DateTimeOffset) { IsFilterable = true }
                };

                // Vector field (use the SDK field properties instead of a non-existent VectorSearchConfiguration)
                var vectorField = new SearchField("contentVector", SearchFieldDataType.Collection(SearchFieldDataType.Single))
                {
                    IsFilterable = false,
                    IsFacetable = false,
                    IsSearchable = true,
                    // set the vector dimension for the field
                    VectorSearchDimensions = _options.VectorDimension,
                    // optionally set a profile name if you plan to reference a profile defined on the index
                    VectorSearchProfileName = "vector-profile"
                };
                

                searchFields.Add(vectorField);

                var index = new SearchIndex(_options.IndexName)
                {
                    Fields = searchFields,


                // Optionally configure index-level vector search settings.
                // Use the parameterless constructor and populate collections rather than attempting a non-existent constructor.
                 VectorSearch = new VectorSearch
                 {
                     Profiles =
                        {
                            new VectorSearchProfile("vector-profile", "hnsw-config")
                            // profile name "vector-profile" must match what you passed above
                        },
                     Algorithms =
                        {
                            new HnswAlgorithmConfiguration("hnsw-config")
                        }
                 }
                };

                // Optionally configure index-level vector search settings.
                // Note: algorithm configuration types may vary between SDK versions; skip adding algorithm parameters here to maintain compatibility.

                // Create or update
                await _indexClient.CreateOrUpdateIndexAsync(index).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to ensure search index exists");
                throw;
            }
        }

        /// <summary>
        /// Uploads or merges documents into the Azure Search index.
        /// </summary>
        public async Task UploadDocumentsAsync(IEnumerable<Document> docs)
        {
            var batch = new IndexDocumentsBatch<Dictionary<string, object>>();

            foreach (var d in docs)
            {
                var doc = new Dictionary<string, object>
                {
                    ["id"] = d.Id.ToString(),
                    ["title"] = d.Title,
                    ["content"] = d.Content,
                    ["sourceDocumentId"] = d.SourceDocumentId.ToString(),
                    ["chunkIndex"] = d.ChunkIndex,
                    ["createdAt"] = d.CreatedAt,
                    ["contentVector"] = d.ContentVector // should be float[]
                };

                batch.Actions.Add(IndexDocumentsAction.MergeOrUpload(doc));
            }

            try
            {
                await _searchClient.IndexDocumentsAsync(batch).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to index documents in Azure Search");
                throw;
            }
        }
    }
}
