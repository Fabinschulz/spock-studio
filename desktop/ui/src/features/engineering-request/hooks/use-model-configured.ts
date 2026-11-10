import { createModelApi } from '@/features/settings';
import type { EngineClient } from '@/shared/api';
import { useAsyncResource } from '@/shared/hooks';
import { useCallback, useMemo } from 'react';

/**
 * Há modelo configurado? `null` enquanto o motor não respondeu. Uma falha ao perguntar conta como "não": o pedido com modelo
 * só se oferece quando o motor disse que pode.
 */
export function useModelConfigured(client: EngineClient): boolean | null {
  const api = useMemo(() => createModelApi(client), [client]);
  const load = useCallback(() => api.status(), [api]);
  const resource = useAsyncResource(load);

  if (resource.status === 'loading') return null;
  return resource.status === 'ready' && resource.value.configured;
}
