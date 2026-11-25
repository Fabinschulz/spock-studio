import type { EngineClient } from '@/shared/api';
import { useAsyncResource } from '@/shared/hooks';
import { useCallback, useEffect } from 'react';

import { readActivityFeed } from '../service/runs-api';
import { ACTIVITY_REFRESH_MS } from './use-run-activity';

/** Relê sozinho enquanto está na tela, mantendo a lista anterior à vista: a atividade muda sem a pessoa pedir. */
export function useActivityFeed(client: EngineClient) {
  const load = useCallback(() => readActivityFeed(client), [client]);
  const feed = useAsyncResource(load, { keepPrevious: true });
  const { reload } = feed;

  useEffect(() => {
    const timer = setInterval(() => {
      if (!document.hidden) reload();
    }, ACTIVITY_REFRESH_MS);
    return () => clearInterval(timer);
  }, [reload]);

  return feed;
}
