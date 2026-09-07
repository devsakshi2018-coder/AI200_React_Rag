import React from 'react';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import SemanticSearch from './SemanticSearch';

jest.mock('../services/documentsApi', () => ({
  searchDocuments: jest.fn()
}));

import { searchDocuments } from '../services/documentsApi';

describe('SemanticSearch', () => {
  afterEach(() => jest.resetAllMocks());

  test('shows "Query is required." when submitting with empty query', async () => {
	render(<SemanticSearch />);
	await userEvent.click(screen.getByRole('button', { name: /search/i }));
	expect(screen.getByRole('alert')).toHaveTextContent('Query is required.');
  });

  test('clamps topK to 50 client-side before calling searchDocuments when user enters a value above 50', async () => {
	(searchDocuments as jest.Mock).mockResolvedValue({ results: [], query: 'q', resultCount: 0 });
	render(<SemanticSearch />);
	await userEvent.type(screen.getByLabelText(/Query/i), 'term');
	await userEvent.clear(screen.getByLabelText(/Top K/i));
	await userEvent.type(screen.getByLabelText(/Top K/i), '100');
	await userEvent.click(screen.getByRole('button', { name: /search/i }));

	await waitFor(() => expect(searchDocuments).toHaveBeenCalledTimes(1));
	const called = (searchDocuments as jest.Mock).mock.calls[0][0];
	expect(called.topK).toBe(50);
  });

  test('renders result title, contentSnippet, and similarity as a percentage on successful search', async () => {
	(searchDocuments as jest.Mock).mockResolvedValue({
	  query: 'term',
	  resultCount: 1,
	  results: [{ documentId: '1', title: 'Doc', contentSnippet: 'abc', similarity: 0.87 }]
	});

	render(<SemanticSearch />);
	await userEvent.type(screen.getByLabelText(/Query/i), 'term');
	await userEvent.click(screen.getByRole('button', { name: /search/i }));

	expect(await screen.findByText('Doc')).toBeInTheDocument();
	expect(screen.getByText('abc')).toBeInTheDocument();
	expect(screen.getByText('87%')).toBeInTheDocument();
  });
});
