import { errorMessage, type EngineClient } from '@/shared/api';
import { useAsyncResource } from '@/shared/hooks';
import type { Discovery, RunSummary } from '@/shared/schema';
import { useCallback } from 'react';

import { readDiscovery, readRuns } from '../service/project-api';

export type ProjectOverview =
  | { status: 'loading' }
  | { status: 'error'; message: string }
  | { status: 'ready'; discovery: Discovery; runs: RunSummary[] };

/** Trocar de projeto descarta a leitura que ainda estava em andamento. */
export function useProjectOverview(client: EngineClient, projectId: string): ProjectOverview {
  const load = useCallback(async () => {
    const [discovery, runs] = await Promise.all([readDiscovery(client, projectId), readRuns(client, projectId)]);
    return { discovery, runs };
  }, [client, projectId]);
  const resource = useAsyncResource(load);

  switch (resource.status) {
    case 'ready':
      return { status: 'ready', ...resource.value };
    case 'error':
      return { status: 'error', message: errorMessage(resource.error, 'Não foi possível ler o projeto.') };
    default:
      return { status: 'loading' };
  }
}
