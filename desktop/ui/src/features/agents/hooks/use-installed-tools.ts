import type { EngineClient } from '@/shared/api';
import { useAsyncResource } from '@/shared/hooks';
import type { AiTool } from '@/shared/schema';
import { useCallback } from 'react';

import { createAgentApi } from '../service/agent-api';

const NONE: readonly AiTool[] = [];

/** As ferramentas de IA instaladas neste computador. Vazio enquanto não se sabe: sem resposta do motor, nada a mostrar. */
export function useInstalledTools(client: EngineClient): readonly AiTool[] {
  const load = useCallback(
    async () => (await createAgentApi(client).tools()).filter((tool) => tool.installed),
    [client]
  );
  const tools = useAsyncResource(load);

  return tools.status === 'ready' ? tools.value : NONE;
}
