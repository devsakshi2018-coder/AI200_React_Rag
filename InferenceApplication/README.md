# Document Ingestion & Semantic Search API

This service ingests documents, generates vector embeddings using Azure OpenAI, stores vectors in PostgreSQL with pgvector, indexes content in Azure Cognitive Search, and exposes a hybrid search endpoint.

Setup notes

- Enable pgvector extension on your Azure Database for PostgreSQL:
  - Connect to your DB and run: `CREATE EXTENSION IF NOT EXISTS vector;`
  - Create the documents table (example):

```sql
CREATE TABLE IF NOT EXISTS documents (
  id UUID PRIMARY KEY,
  title TEXT,
  content TEXT,
  content_vector VECTOR(1536),
  chunk_index INT,
  source_document_id UUID,
  metadata JSONB,
  created_at TIMESTAMPTZ DEFAULT now()
);

-- Create HNSW index
CREATE INDEX IF NOT EXISTS documents_content_vector_idx ON documents USING ivfflat (content_vector vector_cosine_ops) WITH (lists = 100);
```

- Azure OpenAI
  - Create a deployment using `text-embedding-ada-002` or `text-embedding-3-large` and set the deployment name in configuration.

- Azure Cognitive Search
  - The service index will be created at startup by the application. Ensure the API key and endpoint are configured in appsettings or environment variables.

Configuration

- Use appsettings.json or environment variables for sensitive values. Do not hardcode keys.

Running

- dotnet run from the project folder. The API exposes:
  - POST /api/documents/upload (multipart/form-data file)
  - POST /api/documents/search (JSON body: { query, topK })
  - GET /api/documents/health

Notes & TODO

- PDF/DOCX extraction is stubbed; integrate a robust library (PdfPig, OpenXML SDK) for production.
- Tokenization is estimated; consider using an exact tokenizer matching your embedding model for precise chunking.
 - PDF/DOCX extraction implemented using PdfPig (UglyToad.PdfPig) and Open XML SDK (DocumentFormat.OpenXml). Add the following NuGet packages:
   - UglyToad.PdfPig
   - DocumentFormat.OpenXml

Integration tests

- An integration test project using WebApplicationFactory is included under tests/IntegrationTests. It uses mocked IInferenceApplicationService and IDocumentSearchService to validate controller routing and endpoints.

To run tests:

1) Restore and run unit tests:
   - dotnet test tests/InferenceApplication.Tests

2) Run integration tests:
   - dotnet test tests/IntegrationTests

Note: Ensure you run `dotnet restore` to fetch additional packages (PdfPig, OpenXML, test dependencies).
