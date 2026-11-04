import { errorMessage, type EngineClient } from '@/shared/api';
import type { Discovery, RunSummary } from '@/shared/schema';
import { useEffect, useState } from 'react';

import { readDiscovery, readRuns } from '../service/project-api';

export type ProjectOverview =
  | { status: 'loading' }
  | { status: 'error'; message: string }
  | { status: 'ready'; discovery: Discovery; runs: RunSummary[] };

/** Lê a análise e as execuções do projeto. Trocar de projeto descarta a leitura que ainda estava em andamento. */
export function useProjectOverview(client: EngineClient, projectId: string): ProjectOverview {
  const [state, setState] = useState<{ projectId: string; value: ProjectOverview }>({
    projectId,
    value: { status: 'loading' }
  });

  useEffect(() => {
    let active = true;
    Promise.all([readDiscovery(client, projectId), readRuns(client, projectId)])
      .then(([discovery, runs]) => {
        if (active) setState({ projectId, value: { status: 'ready', discovery, runs } });
      })
      .catch((cause: unknown) => {
        if (active)
          setState({
            projectId,
            value: { status: 'error', message: errorMessage(cause, 'Não foi possível ler o projeto.') }
          });
      });
    return () => {
      active = false;
    };
  }, [client, projectId]);

  return state.projectId === projectId ? state.value : { status: 'loading' };
}
