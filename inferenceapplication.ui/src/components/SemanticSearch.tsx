import React, { useState } from 'react';
import { searchDocuments } from '../services/documentsApi';
import type { SearchResultItem } from '../types/documents';

export const SemanticSearch: React.FC = () => {
  const [query, setQuery] = useState('');
  const [topK, setTopK] = useState<number>(5);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [results, setResults] = useState<SearchResultItem[]>([]);

  const onSubmit = async (e: React.FormEvent) => {
	e.preventDefault();
	setError(null);
	setResults([]);
	if (!query.trim()) {
	  setError('Query is required.');
	  return;
	}
	const clamped = Math.min(50, Math.max(1, Math.floor(Number(topK) || 5)));
	setLoading(true);
	try {
	  const resp = await searchDocuments({ query: query.trim(), topK: clamped });
	  setResults(resp.results);
	} catch (err: unknown) {
	  setError((err as Error).message || 'Search failed');
	} finally {
	  setLoading(false);
	}
  };

  return (
	<form onSubmit={onSubmit}>
	  <div>
		<label htmlFor="query-input">Query</label>
		<input id="query-input" value={query} onChange={(e) => setQuery(e.target.value)} />
	  </div>
	  <div>
		<label htmlFor="topk-input">Top K</label>
		<input id="topk-input" type="number" value={topK} onChange={(e) => setTopK(Number(e.target.value))} />
	  </div>
	  <button type="submit" disabled={loading}>Search</button>
	  {loading && <div role="status">Searching...</div>}
	  {error && <div role="alert">{error}</div>}
	  <div>
		{results.map((r) => (
		  <div key={r.documentId} data-testid="search-result">
			<h4>{r.title}</h4>
			<p>{r.contentSnippet}</p>
			<div>{Math.round(r.similarity * 100)}%</div>
		  </div>
		))}
	  </div>
	</form>
  );
};

export default SemanticSearch;
