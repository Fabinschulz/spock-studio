import type { EngineClient } from '@/shared/api';
import {
  portfolioSchema,
  projectForecastSchema,
  projectMetricsSchema,
  projectRisksSchema,
  type Portfolio,
  type ProjectForecast,
  type ProjectMetrics,
  type ProjectRisks
} from '@/shared/schema';

/** A visão gerencial do motor: só leitura do que já foi sincronizado. */
export type ManagementApi = {
  portfolio(): Promise<Portfolio>;
  metrics(projectId: string, weeks?: number): Promise<ProjectMetrics>;
  forecast(projectId: string): Promise<ProjectForecast>;
  risks(projectId: string): Promise<ProjectRisks>;
};

export function createManagementApi(client: EngineClient): ManagementApi {
  return {
    portfolio: () => client.request('management.portfolio', {}, portfolioSchema),
    metrics: (projectId, weeks) =>
      client.request('management.metrics', weeks ? { projectId, weeks } : { projectId }, projectMetricsSchema),
    forecast: (projectId) => client.request('management.forecast', { projectId }, projectForecastSchema),
    risks: (projectId) => client.request('management.risks', { projectId }, projectRisksSchema)
  };
}
