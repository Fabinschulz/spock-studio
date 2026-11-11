import type { EngineClient } from '@/shared/api';
import { portfolioSchema, projectMetricsSchema, type Portfolio, type ProjectMetrics } from '@/shared/schema';

/** A visão gerencial do motor: só leitura do que já foi sincronizado. */
export type ManagementApi = {
  portfolio(): Promise<Portfolio>;
  metrics(projectId: string, weeks?: number): Promise<ProjectMetrics>;
};

export function createManagementApi(client: EngineClient): ManagementApi {
  return {
    portfolio: () => client.request('management.portfolio', {}, portfolioSchema),
    metrics: (projectId, weeks) =>
      client.request('management.metrics', weeks ? { projectId, weeks } : { projectId }, projectMetricsSchema)
  };
}
