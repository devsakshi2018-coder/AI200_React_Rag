import { INFERENCE_API_URL } from '../config/env';
import type {
  HealthResponse,
  UploadDocumentResponse,
  SearchRequest,
  SearchResponse
} from '../types/documents';

export class DocumentsApiError extends Error {
  public status: number;
  constructor(message: string, status: number) {
	super(message);
	this.name = 'DocumentsApiError';
	this.status = status;
  }
}

const buildUrl = (path: string) => {
  const base = INFERENCE_API_URL ?? '';
  return `${base.replace(/\/$/, '')}${path}`;
};

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const res = await fetch(buildUrl(path), init);
  if (res.ok) {
	const text = await res.text();
	try {
	  return text ? JSON.parse(text) : (undefined as unknown as T);
	} catch {
	  // not JSON
	  return (text as unknown) as T;
	}
  }

  // Non-2xx: try to parse body intelligently
  const contentType = res.headers.get('content-type') ?? '';
  let message = '';
  try {
	if (contentType.includes('application/json')) {
	  const obj = await res.json();
	  // ProblemDetails object { title, detail, status }
	  if (obj && (obj.title || obj.detail)) {
		message = obj.title ? `${obj.title}${obj.detail ? `: ${obj.detail}` : ''}` : JSON.stringify(obj);
	  } else {
		message = JSON.stringify(obj);
	  }
	} else {
	  message = await res.text();
	}
  } catch (e) {
	message = res.statusText || 'Unknown error';
  }

  throw new DocumentsApiError(message, res.status);
}

export function getHealth(): Promise<HealthResponse> {
  return request<HealthResponse>('/api/documents/health');
}

export function uploadDocument(file: File, title: string): Promise<UploadDocumentResponse> {
  const fd = new FormData();
  fd.append('file', file);
  fd.append('title', title);
  return request<UploadDocumentResponse>('/api/documents/upload', {
	method: 'POST',
	body: fd
  });
}

export function searchDocuments(requestBody: SearchRequest): Promise<SearchResponse> {
  const body = JSON.stringify(requestBody);
  return request<SearchResponse>('/api/documents/search', {
	method: 'POST',
	headers: { 'Content-Type': 'application/json' },
	body
  });
}
