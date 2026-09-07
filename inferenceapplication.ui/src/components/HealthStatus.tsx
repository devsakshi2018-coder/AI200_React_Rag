import React, { useEffect, useState } from 'react';
import { getHealth } from '../services/documentsApi';
import type { HealthResponse } from '../types/documents';

export const HealthStatus: React.FC = () => {
  const [loading, setLoading] = useState(true);
  const [health, setHealth] = useState<HealthResponse | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
	let mounted = true;
	getHealth()
	  .then((h) => {
		if (!mounted) return;
		setHealth(h);
	  })
	  .catch((e: unknown) => {
		if (!mounted) return;
		setError((e as Error).message ?? 'Error');
	  })
	  .finally(() => {
		if (!mounted) return;
		setLoading(false);
	  });
	return () => {
	  mounted = false;
	};
  }, []);

  if (loading) return <div role="status">Loading...</div>;
  if (error) return <div role="status">Error: {error}</div>;
  return <div role="status">{health?.status ?? 'Unknown'}</div>;
};

export default HealthStatus;
