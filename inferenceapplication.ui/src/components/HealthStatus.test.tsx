import React from 'react';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import HealthStatus from './HealthStatus';

jest.mock('../services/documentsApi', () => ({
  getHealth: jest.fn()
}));

import { getHealth } from '../services/documentsApi';
import type { HealthResponse } from '../types/documents';

describe('HealthStatus', () => {
  afterEach(() => jest.resetAllMocks());

  test('shows a loading state before the API call resolves', async () => {
	let resolve: (h: HealthResponse) => void = () => {};
	const p = new Promise<HealthResponse>((res) => { resolve = res; });
	(getHealth as jest.Mock).mockReturnValue(p);

	render(<HealthStatus />);
	expect(screen.getByRole('status')).toHaveTextContent('Loading...');

	resolve({ status: 'Healthy', checkedAtUtc: '2026-09-06T10:00:00Z' });
	await waitFor(() => expect(screen.getByRole('status')).toHaveTextContent('Healthy'));
  });

  test('shows healthy status text on success', async () => {
	(getHealth as jest.Mock).mockResolvedValue({ status: 'Healthy', checkedAtUtc: '2026-09-06T10:00:00Z' });
	render(<HealthStatus />);
	expect(await screen.findByRole('status')).toHaveTextContent('Healthy');
  });

  test('shows error status text on failure', async () => {
	(getHealth as jest.Mock).mockRejectedValue(new Error('Service down'));
	render(<HealthStatus />);
	expect(await screen.findByRole('status')).toHaveTextContent('Error: Service down');
  });
});
