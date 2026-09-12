using System;
using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Polly;
using Polly.Extensions.Http;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

// -----------------------------------------------------------------
// 1) Managed Identity based BlobServiceClient
//    Storage account name comes from config, NOT connection string.
//    Requires: "Storage Blob Data Reader" (or Contributor) role
//    assigned to the Function App's Managed Identity on the Storage Account.
// -----------------------------------------------------------------
builder.Services.AddAzureClients(clientBuilder =>
{
    var storageAccountUri = builder.Configuration["StorageAccountBlobUri"];
    // e.g. "https://<account>.blob.core.windows.net"
    clientBuilder.AddBlobServiceClient(new Uri(storageAccountUri!));
    clientBuilder.UseCredential(new DefaultAzureCredential());
});

// -----------------------------------------------------------------
// 2) HttpClient (named) for calling the Container App Upload API
//    + Polly retry & timeout policies
// -----------------------------------------------------------------
builder.Services.AddHttpClient("ContainerAppUploadClient", client =>
{
    var baseUrl = builder.Configuration["ContainerApp:UploadApiBaseUrl"];
    client.BaseAddress = new Uri(baseUrl!);
    client.Timeout = TimeSpan.FromSeconds(100);
})
.AddPolicyHandler(GetRetryPolicy())
.AddPolicyHandler(GetTimeoutPolicy());

// -----------------------------------------------------------------
// 3) Register the service that holds "find latest blob + upload" logic
// -----------------------------------------------------------------
builder.Services.AddSingleton<BlobUploadService>();

// TokenCredential used to fetch AAD tokens for calling the Container App
// (Container App API must validate this JWT itself — see BlobUploadService)
builder.Services.AddSingleton<DefaultAzureCredential>();

builder.Build().Run();

static IAsyncPolicy<System.Net.Http.HttpResponseMessage> GetRetryPolicy()
{
    return HttpPolicyExtensions
        .HandleTransientHttpError() // 5xx, 408, HttpRequestException
        .WaitAndRetryAsync(
            retryCount: 3,
            sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)) // 2s, 4s, 8s
        );
}

static IAsyncPolicy<System.Net.Http.HttpResponseMessage> GetTimeoutPolicy()
{
    return Policy.TimeoutAsync<System.Net.Http.HttpResponseMessage>(TimeSpan.FromSeconds(100));
}
