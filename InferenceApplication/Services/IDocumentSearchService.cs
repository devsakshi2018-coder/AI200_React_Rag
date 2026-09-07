using System.Collections.Generic;
using System.Threading.Tasks;
using InferenceApplication.Models;

namespace InferenceApplication.Services
{
    /// <summary>
    /// Abstraction for document search operations.
    /// </summary>
    public interface IDocumentSearchService
    {
        Task<List<SearchResultItem>> SearchAsync(string query, int topK = 5);
    }
}
