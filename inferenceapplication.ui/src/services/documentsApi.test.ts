import { jest } from '@jest/globals';
import { DocumentsApiError, getHealth, uploadDocument, searchDocuments } from './documentsApi';
import type { UploadDocumentResponse, HealthResponse, SearchResponse } from '../types/documents';

describe('documentsApi', () => {
  const originalFetch = global.fetch;

  afterEach(() => {
	global.fetch = originalFetch;
	jest.resetAllMocks();
  });

  test('getHealth: success returns parsed response', async () => {
	const resp: HealthResponse = { status: 'Healthy', checkedAtUtc: '2026-09-06T10:00:00Z' };
	global.fetch = jest.fn().mockResolvedValue(new Response(JSON.stringify(resp), { status: 200 }));

	const result = await getHealth();
	expect(result).toEqual(resp);
  });

  test('getHealth: failure throws DocumentsApiError', async () => {
	global.fetch = jest.fn().mockResolvedValue(new Response('Service down', { status: 500, headers: { 'Content-Type': 'text/plain' } }));

	await expect(getHealth()).rejects.toMatchObject({ instanceof: DocumentsApiError, status: 500, message: 'Service down' });
  });

  test('uploadDocument: sends FormData with file and title and returns parsed response', async () => {
	const file = new File(['content'], 'test.txt', { type: 'text/plain' });
	const title = 'My Doc';
	const resp: UploadDocumentResponse = {
	  documentId: 'id',
	  title,
	  fileName: 'test.txt',
	  fileSizeBytes: 7,
	  uploadedAtUtc: '2026-09-06T10:00:00Z'
	};

	global.fetch = jest.fn().mockImplementation((_input: RequestInfo, init?: RequestInit) => {
	  const body = init?.body as FormData;
	  expect(body).toBeInstanceOf(FormData);
	  expect(body.get('title')).toBe(title);
	  expect(body.get('file')).toBe(file);
	  return Promise.resolve(new Response(JSON.stringify(resp), { status: 200 }));
	});

	const result = await uploadDocument(file, title);
	expect(result).toEqual(resp);
  });

  test('uploadDocument: backend 400 error message surfaced verbatim', async () => {
	const file = new File(['content'], 'test.txt');
	const title = 'My Doc';
	global.fetch = jest.fn().mockResolvedValue(new Response('Bad request: missing fields', { status: 400, headers: { 'Content-Type': 'text/plain' } }));

	await expect(uploadDocument(file, title)).rejects.toMatchObject({ instanceof: DocumentsApiError, status: 400, message: 'Bad request: missing fields' });
  });

  test('searchDocuments: success returns parsed response', async () => {
	const resp: SearchResponse = {
	  query: 'q',
	  resultCount: 1,
	  results: [
		{ documentId: 'd1', title: 'T', contentSnippet: 'snippet', similarity: 0.5 }
	  ]
	};

	global.fetch = jest.fn().mockResolvedValue(new Response(JSON.stringify(resp), { status: 200 }));

	const result = await searchDocuments({ query: 'q', topK: 5 });
	expect(result).toEqual(resp);
  });

  test('searchDocuments: backend 400 error message surfaced verbatim', async () => {
	global.fetch = jest.fn().mockResolvedValue(new Response('TopK must be between 1 and 50.', { status: 400, headers: { 'Content-Type': 'text/plain' } }));

	await expect(searchDocuments({ query: 'q', topK: 100 })).rejects.toMatchObject({ instanceof: DocumentsApiError, status: 400, message: 'TopK must be between 1 and 50.' });
  });
});
