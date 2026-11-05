import type { EngineClient } from '@/shared/api';
import { createModelApi } from '@/features/settings';
import { useEffect, useMemo, useState } from 'react';

/**
 * Há modelo configurado? `null` enquanto o motor não respondeu. Uma falha ao perguntar conta como "não": o pedido com modelo
 * só se oferece quando o motor disse que pode.
 */
export function useModelConfigured(client: EngineClient): boolean | null {
  const api = useMemo(() => createModelApi(client), [client]);
  const [configured, setConfigured] = useState<boolean | null>(null);

  useEffect(() => {
    let active = true;
    api
      .status()
      .then((status) => {
        if (active) setConfigured(status.configured);
      })
      .catch(() => {
        if (active) setConfigured(false);
      });
    return () => {
      active = false;
    };
  }, [api]);

  return configured;
}
