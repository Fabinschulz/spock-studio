import { createIntegrationApi } from '@/features/integrations';
import { readHarness } from '@/features/project';
import { createModelApi } from '@/features/settings';
import type { EngineClient } from '@/shared/api';
import { useAsyncResource } from '@/shared/hooks';
import { useCallback } from 'react';

import type { SetupStatus } from './steps';

type EngineFacts = Pick<SetupStatus, 'prepare' | 'model' | 'tools'>;

const UNKNOWN: EngineFacts = { prepare: null, model: null, tools: null };

const settled = <T>(result: PromiseSettledResult<T>): T | null => (result.status === 'fulfilled' ? result.value : null);

/**
 * Cada passo é perguntado ao motor, e uma pergunta que falha deixa só aquele passo sem resposta: o início não mostra
 * erro por causa de um resumo, e nada aparece como feito sem confirmação.
 */
export function useSetupStatus(client: EngineClient, projectId: string | null, enabled: boolean): SetupStatus {
  const load = useCallback(async (): Promise<EngineFacts> => {
    if (!enabled) return UNKNOWN;
    const [model, connections, harness] = await Promise.allSettled([
      createModelApi(client).status(),
      createIntegrationApi(client).connections(),
      projectId ? readHarness(client, projectId) : Promise.resolve(null)
    ]);
    const connected = settled(connections);
    return {
      model: settled(model)?.configured ?? null,
      tools: connected ? connected.length > 0 : null,
      prepare: projectId ? (settled(harness)?.applied ?? null) : false
    };
  }, [client, projectId, enabled]);
  const facts = useAsyncResource(load);

  return { project: projectId !== null, ...(facts.status === 'ready' ? facts.value : UNKNOWN) };
}
