using System;
using System.IO;
using System.Threading.Tasks;
using InferenceApplication.Models;
using Azure;
using InferenceApplication.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace InferenceApplication.Controllers
{
    [ApiController]
    [Route("api/documents")]
    public class DocumentsController : ControllerBase
    {
        private readonly IInferenceApplicationService _ingestionService;
        private readonly IDocumentSearchService _searchService;
        private readonly ILogger<DocumentsController> _logger;

        public DocumentsController(IInferenceApplicationService ingestionService, IDocumentSearchService searchService, ILogger<DocumentsController> logger)
        {
            _ingestionService = ingestionService;
            _searchService = searchService;
            _logger = logger;
        }

        /// <summary>
        /// Uploads a document (multipart/form-data). Supports .txt, .pdf, .docx.
        /// </summary>
        [HttpPost("upload")]
        [RequestSizeLimit(50_000_000)] // 50 MB
        public async Task<IActionResult> Upload([FromForm] DocumentUploadRequest request)
        {
            if (request.File == null) return BadRequest("File is required.");
            if (request.File.Length == 0) return BadRequest("Empty file.");

            try
            {
                await using var stream = request.File.OpenReadStream();
                var sourceId = await _ingestionService.IngestAsync(stream, request.File.FileName, request.Title ?? request.File.FileName).ConfigureAwait(false);
                return Accepted(new { sourceDocumentId = sourceId });
            }
            catch (NotSupportedException ex)
            {
                _logger.LogWarning(ex, "Unsupported file");
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ingestion failed");
                return StatusCode(500, "Failed to ingest document.");
            }
        }

        /// <summary>
        /// Performs a semantic hybrid search over uploaded documents.
        /// </summary>
        [HttpPost("search")]
        public async Task<IActionResult> Search([FromBody] SearchRequest request)
        {
            if (string.IsNullOrWhiteSpace(request?.Query)) return BadRequest("Query is required.");

            try
            {
                var results = await _searchService.SearchAsync(request.Query, request.TopK).ConfigureAwait(false);
                return Ok(results);
            }
            catch (RequestFailedException rfe) when (rfe.Status == 429)
            {
                return StatusCode(429, "Rate limited by search provider.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Search failed");
                return StatusCode(500, "Search failed.");
            }
        }

        /// <summary>
        /// Basic health check endpoint.
        /// </summary>
        [HttpGet("health")]
        public IActionResult Health() => Ok(new { status = "healthy" });
    }
}
