using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace InferenceApplication.Services
{
    /// <summary>
    /// Abstraction for document ingestion orchestration.
    /// </summary>
    public interface IInferenceApplicationService
    {
        Task<Guid> IngestAsync(Stream fileStream, string fileName, string title, Dictionary<string, object>? metadata = null, CancellationToken cancellationToken = default);
    }
}
