using System;
using InferenceApplication.Repositories;
using InferenceApplication.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;


var builder = WebApplication.CreateBuilder(args);

// Configuration
builder.Services.Configure<PostgresOptions>(builder.Configuration.GetSection("Postgres"));
builder.Services.Configure<EmbeddingOptions>(builder.Configuration.GetSection("AzureOpenAI"));
builder.Services.Configure<SearchOptions>(builder.Configuration.GetSection("AzureSearch"));
builder.Services.Configure<IngestionOptions>(builder.Configuration.GetSection("Ingestion"));

// DI registrations
builder.Services.AddSingleton<EmbeddingService>();
builder.Services.AddSingleton<SearchIndexService>();
builder.Services.AddScoped<DocumentRepository>();
builder.Services.AddScoped<TextChunker>();
builder.Services.AddScoped<IInferenceApplicationService, InferenceApplicationService>();
builder.Services.AddScoped<IDocumentSearchService, DocumentSearchService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Health checks (basic)
builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetValue<string>("Postgres:ConnectionString"), name: "postgres")
    ;

var app = builder.Build();

// Ensure search index exists at startup
using (var scope = app.Services.CreateScope())
{
    var indexSvc = scope.ServiceProvider.GetRequiredService<SearchIndexService>();
    await indexSvc.EnsureIndexExistsAsync();
}

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRouting();
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/healthz");

app.Run();
