import React from 'react';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import DocumentUpload from './DocumentUpload';

jest.mock('../services/documentsApi', () => ({
  uploadDocument: jest.fn()
}));

import { uploadDocument } from '../services/documentsApi';

describe('DocumentUpload', () => {
  afterEach(() => jest.resetAllMocks());

  test('shows "Title is required." when submitting with no title', async () => {
	render(<DocumentUpload />);
	await userEvent.click(screen.getByRole('button', { name: /upload/i }));
	expect(screen.getByRole('alert')).toHaveTextContent('Title is required.');
  });

  test('shows "A file is required." when submitting with no file and valid title', async () => {
	render(<DocumentUpload />);
	await userEvent.type(screen.getByLabelText(/Title/i), 'My Title');
	await userEvent.click(screen.getByRole('button', { name: /upload/i }));
	expect(screen.getByRole('alert')).toHaveTextContent('A file is required.');
  });

  test('calls uploadDocument with the exact file and title on valid submit, and shows success text', async () => {
	const file = new File(['hello'], 'hello.txt', { type: 'text/plain' });
	(uploadDocument as jest.Mock).mockResolvedValue({});

	render(<DocumentUpload />);
	const titleInput = screen.getByLabelText(/Title/i);
	const fileInput = screen.getByLabelText(/File/i) as HTMLInputElement;

	await userEvent.type(titleInput, 'My Title');
	// fire input files
	await userEvent.upload(fileInput, file);
	await userEvent.click(screen.getByRole('button', { name: /upload/i }));

	await waitFor(() => expect(uploadDocument).toHaveBeenCalledTimes(1));
	const [calledFile, calledTitle] = (uploadDocument as jest.Mock).mock.calls[0];
	expect(calledTitle).toBe('My Title');
	expect(calledFile).toBeInstanceOf(File);
	expect((calledFile as File).name).toBe('hello.txt');

	expect(await screen.findByRole('status')).toHaveTextContent('success');
  });

  test('shows the exact API error message on upload failure', async () => {
	(uploadDocument as jest.Mock).mockRejectedValue(new Error('Backend says no'));
	render(<DocumentUpload />);
	await userEvent.type(screen.getByLabelText(/Title/i), 'My Title');
	const file = new File(['x'], 'x.txt');
	const fileInput = screen.getByLabelText(/File/i) as HTMLInputElement;
	await userEvent.upload(fileInput, file);
	await userEvent.click(screen.getByRole('button', { name: /upload/i }));

	expect(await screen.findByRole('alert')).toHaveTextContent('Backend says no');
  });
});
