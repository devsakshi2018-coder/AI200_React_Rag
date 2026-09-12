using System;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace UploadBlobFile;

public class BlobTriggerFunction
{
    private readonly BlobUploadService _blobUploadService;
    private readonly ILogger<BlobTriggerFunction> _logger;

    // Container name is embedded in the trigger path below.
    // Change "incoming-files" to your actual container name,
    // or bind it via %ContainerName% app setting.
    private const string ContainerName = "incoming-files";

    public BlobTriggerFunction(
        BlobUploadService blobUploadService,
        ILogger<BlobTriggerFunction> logger)
    {
        _blobUploadService = blobUploadService;
        _logger = logger;
    }

    [Function(nameof(BlobTriggerFunction))]
    public async Task Run(
        [BlobTrigger("incoming-files/{name}", Connection = "StorageAccountConnection")]
        Stream triggerStream,
        string name)
    {
        _logger.LogInformation(
            "Blob trigger fired for '{Name}'. Re-scanning container for the LATEST blob...", name);

        try
        {
            // Per requirement: always process the *latest* blob in the
            // container, not necessarily the one that fired the trigger
            // (handles near-simultaneous uploads gracefully).
            await _blobUploadService.ProcessLatestBlobAsync(ContainerName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to process latest blob in container '{Container}' after trigger on '{Name}'.",
                ContainerName, name);
            // Optionally: write a marker to a "failed-uploads" queue/container here.
            throw; // let the Functions runtime retry per host.json policy
        }
    }
}
