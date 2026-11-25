import type { EngineClient } from '@/shared/api';
import { useAsyncResource } from '@/shared/hooks';
import { useCallback } from 'react';

import { createAgentApi } from '../service/agent-api';

/**
 * Há alguma ferramenta de IA instalada neste computador? `null` enquanto não se sabe (motor ainda não pronto, leitura
 * em andamento ou falha): quem pergunta só avisa a falta quando o motor disse que não há.
 */
export function useAgentInstalled(client: EngineClient, enabled: boolean): boolean | null {
  const load = useCallback(
    async () => (enabled ? (await createAgentApi(client).tools()).some((tool) => tool.installed) : null),
    [client, enabled]
  );
  const tools = useAsyncResource(load);

  return tools.status === 'ready' ? tools.value : null;
}
