import React, { useState } from 'react';
import { uploadDocument } from '../services/documentsApi';

export const DocumentUpload: React.FC = () => {
  const [title, setTitle] = useState('');
  const [file, setFile] = useState<File | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);

  const onSubmit = async (e: React.FormEvent) => {
	e.preventDefault();
	setError(null);
	setSuccess(null);

	if (!title.trim()) {
	  setError('Title is required.');
	  return;
	}
	if (!file) {
	  setError('A file is required.');
	  return;
	}

	setLoading(true);
	try {
	  await uploadDocument(file, title);
	  setSuccess('Upload success');
	} catch (err: unknown) {
	  setError((err as Error).message || 'Upload failed');
	} finally {
	  setLoading(false);
	}
  };

  return (
	<form onSubmit={onSubmit}>
	  <div>
		<label htmlFor="file-input">File</label>
		<input id="file-input" aria-label="File" type="file" onChange={(e) => setFile(e.target.files?.[0] ?? null)} />
	  </div>
	  <div>
		<label htmlFor="title-input">Title</label>
		<input id="title-input" aria-label="Title" value={title} onChange={(e) => setTitle(e.target.value)} />
	  </div>
	  <button type="submit" disabled={loading}>Upload</button>
	  {loading && <div role="status">Uploading...</div>}
	  {error && <div role="alert">{error}</div>}
	  {success && <div role="status">{success}</div>}
	</form>
  );
};

export default DocumentUpload;
