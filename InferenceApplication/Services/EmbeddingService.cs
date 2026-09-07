using Azure;
using Azure.AI.OpenAI;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Embeddings;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace InferenceApplication.Services
{
    /// <summary>
    /// Options for Azure OpenAI embedding generation.
    /// </summary>
    public class EmbeddingOptions
    {
        public string Endpoint { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty; // prefer managed identity in prod
        public string DeploymentName { get; set; } = "text-embedding-ada-002";
        public int BatchSize { get; set; } = 16;
        public int MaxRetries { get; set; } = 5;
    }

    /// <summary>
    /// Service that generates embeddings using Azure OpenAI.
    /// </summary>
    public class EmbeddingService
    {
        private readonly AzureOpenAIClient _client;
        private readonly EmbeddingOptions _options;
        private readonly ILogger<EmbeddingService> _logger;

        /// <summary>
        /// Constructs a new EmbeddingService instance.
        /// </summary>
        public EmbeddingService(IOptions<EmbeddingOptions> options, ILogger<EmbeddingService> logger)
        {
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            if (string.IsNullOrWhiteSpace(_options.Endpoint))
            {
                throw new ArgumentException("Endpoint must be supplied in EmbeddingOptions", nameof(options));
            }

            if (!string.IsNullOrWhiteSpace(_options.ApiKey))
            {
                var credential = new AzureKeyCredential(_options.ApiKey);
                _client = new AzureOpenAIClient(new Uri(_options.Endpoint), credential);
            }
            else
            {
                // Use DefaultAzureCredential (Managed Identity, environment, etc.) when ApiKey not supplied
                var credential = new DefaultAzureCredential();
                _client = new AzureOpenAIClient(new Uri(_options.Endpoint), credential);
            }
        }

        /// <summary>
        /// Gets an embedding vector for the provided input text.
        /// </summary>
        public async Task<float[]> GetEmbeddingAsync(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                throw new ArgumentException("Input must not be null or empty", nameof(input));
            }

            var model = string.IsNullOrWhiteSpace(_options?.DeploymentName) ? "text-embedding-ada-002" : _options.DeploymentName;
            var embeddingClient = _client.GetEmbeddingClient(model);

            OpenAIEmbedding embedding = await embeddingClient.GenerateEmbeddingAsync(input).ConfigureAwait(false);

            if (embedding == null)
            {
                throw new InvalidOperationException("Embedding response did not contain data.");
            }

            var vector = embedding.ToFloats();
            if (vector.Length == 0)
            {
                return Array.Empty<float>();
            }

            return vector.ToArray();
        }
    }
}
