import { useAsyncResource } from '@/shared/hooks';
import { useCallback } from 'react';

import { revertibleApply, type ConfiguredProject } from '../domain/configured-project';
import type { HarnessApi } from '../service';

/**
 * O que já está aplicado no projeto, para a segunda visita. `refresh` muda a cada execução terminada, para o resumo
 * não ficar com o estado de antes. Falhar aqui só esconde o resumo: a jornada segue pela análise.
 */
export function useConfiguredProject(api: HarnessApi, projectId: string, refresh: unknown): ConfiguredProject | null {
  const load = useCallback(async (): Promise<ConfiguredProject | null> => {
    void refresh;
    const [status, runs] = await Promise.all([api.status(projectId), api.runs(projectId)]);
    return status.applied ? { status, revertible: revertibleApply(runs) } : null;
  }, [api, projectId, refresh]);
  const resource = useAsyncResource(load, { keepPrevious: true });
  return resource.status === 'ready' ? resource.value : null;
}
