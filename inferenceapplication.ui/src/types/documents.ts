export interface HealthResponse {
  status: string;
  checkedAtUtc: string;
}

export interface UploadDocumentResponse {
  documentId: string;
  title: string;
  fileName: string;
  fileSizeBytes: number;
  uploadedAtUtc: string;
}

export interface SearchRequest {
  query: string;
  topK?: number;
}

export interface SearchResultItem {
  documentId: string;
  title: string;
  contentSnippet: string;
  similarity: number;
}

export interface SearchResponse {
  query: string;
  resultCount: number;
  results: SearchResultItem[];
}
