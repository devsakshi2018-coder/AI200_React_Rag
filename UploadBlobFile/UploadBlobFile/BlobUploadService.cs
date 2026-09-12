using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

public class BlobUploadService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly DefaultAzureCredential _credential;
    private readonly ILogger<BlobUploadService> _logger;

    // Scope/Audience for the Container App's App Registration.
    // Container App must be configured to validate AAD JWT tokens
    // with this same Application (client) ID as audience.
    private readonly string _containerAppScope;

    // Max allowed file size for upload (50 MB)
    private const long MaxFileSizeBytes = 50L * 1024 * 1024;

    public BlobUploadService(
        BlobServiceClient blobServiceClient,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        DefaultAzureCredential credential,
        ILogger<BlobUploadService> logger)
    {
        _blobServiceClient = blobServiceClient;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _credential = credential;
        _logger = logger;

        // e.g. "api://<container-app-client-id>/.default"
        //_containerAppScope = _configuration["ContainerApp:TokenScope"]
        //    ?? throw new InvalidOperationException("ContainerApp:TokenScope is not configured");
    }

    /// <summary>
    /// Finds the most recently modified blob in the container and uploads
    /// it to the Container App's upload endpoint.
    /// </summary>
    public async Task ProcessLatestBlobAsync(string containerName, CancellationToken ct = default)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);

        BlobItem? latestBlob = null;

        await foreach (var blobItem in containerClient.GetBlobsAsync(
            traits: BlobTraits.Metadata, cancellationToken: ct))
        {
            if (latestBlob == null ||
                blobItem.Properties.LastModified > latestBlob.Properties.LastModified)
            {
                latestBlob = blobItem;
            }
        }

        if (latestBlob == null)
        {
            _logger.LogWarning("Container '{Container}' has no blobs to process.", containerName);
            return;
        }

        _logger.LogInformation(
            "Latest blob found: {Name}, LastModified: {LastModified}, Size: {Size} bytes",
            latestBlob.Name, latestBlob.Properties.LastModified, latestBlob.Properties.ContentLength);

        // ---- 50 MB max size guard ----
        var blobSizeBytes = latestBlob.Properties.ContentLength ?? 0;
        if (blobSizeBytes > MaxFileSizeBytes)
        {
            _logger.LogError(
                "Blob '{Name}' is {SizeMb:F2} MB, which exceeds the 50 MB upload limit. Skipping upload.",
                latestBlob.Name, blobSizeBytes / (1024.0 * 1024.0));

            // Optionally: move/copy this blob to a "rejected-oversize" container
            // or write a marker so it isn't repeatedly re-evaluated on every trigger.
            return;
        }

        var blobClient = containerClient.GetBlobClient(latestBlob.Name);

        // Idempotency guard (simple example — swap for Table Storage /
        // a DB check in real usage so restarts don't re-upload).
        var alreadyProcessedTag = "processedByUploadFunction";
        var blobProperties = await blobClient.GetPropertiesAsync(cancellationToken: ct);
        if (blobProperties.Value.Metadata.ContainsKey(alreadyProcessedTag))
        {
            _logger.LogInformation("Blob '{Name}' already processed. Skipping.", latestBlob.Name);
            return;
        }

        await using var stream = await blobClient.OpenReadAsync(cancellationToken: ct);

        await UploadToContainerAppAsync(
            stream,
            fileName: latestBlob.Name,
            contentType: latestBlob.Properties.ContentType ?? "application/octet-stream",
            ct);

        // Mark as processed so retriggers / reruns don't duplicate the upload
        var updatedMetadata = new System.Collections.Generic.Dictionary<string, string>(
            blobProperties.Value.Metadata)
        {
            [alreadyProcessedTag] = DateTimeOffset.UtcNow.ToString("O")
        };
        await blobClient.SetMetadataAsync(updatedMetadata, cancellationToken: ct);
    }

    private async Task UploadToContainerAppAsync(
        Stream fileStream, string fileName, string contentType, CancellationToken ct)
    {
        var httpClient = _httpClientFactory.CreateClient("ContainerAppUploadClient");

        // ---- Managed Identity: acquire AAD token for the Container App ----
        var tokenRequestContext = new TokenRequestContext(new[] { _containerAppScope });
        AccessToken token = await _credential.GetTokenAsync(tokenRequestContext, ct);
        httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token.Token);

        using var content = new MultipartFormDataContent();
        using var streamContent = new StreamContent(fileStream);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(streamContent, "file", fileName);

        var uploadPath = _configuration["ContainerApp:UploadEndpointPath"] ?? "/api/upload";

        _logger.LogInformation("Uploading blob '{FileName}' to Container App endpoint {Path}",
            fileName, uploadPath);

        var response = await httpClient.PostAsync(uploadPath, content, ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError(
                "Upload failed for '{FileName}'. Status: {Status}. Body: {Body}",
                fileName, response.StatusCode, body);
            response.EnsureSuccessStatusCode(); // throws -> triggers Polly retry upstream if wrapped
        }

        _logger.LogInformation("Upload succeeded for '{FileName}'.", fileName);
    }

    /*
     * ALTERNATE UPLOAD FORMAT (if the API expects raw binary body
     * instead of multipart/form-data):
     *
     * using var streamContent = new StreamContent(fileStream);
     * streamContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
     * httpClient.DefaultRequestHeaders.Add("X-File-Name", fileName);
     * var response = await httpClient.PostAsync(uploadPath, streamContent, ct);
     */
}
