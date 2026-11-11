import type { EngineClient } from '@/shared/api';
import {
  assistantAnswerSchema,
  evidencePackSchema,
  modelStatusSchema,
  portfolioSchema,
  projectForecastSchema,
  projectMetricsSchema,
  projectRisksSchema,
  type AssistantAnswer,
  type EvidencePack,
  type ModelStatus,
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
  modelStatus(): Promise<ModelStatus>;
  evidence(projectId: string): Promise<EvidencePack>;
  ask(projectId: string, question: string): Promise<AssistantAnswer>;
};

export function createManagementApi(client: EngineClient): ManagementApi {
  return {
    portfolio: () => client.request('management.portfolio', {}, portfolioSchema),
    metrics: (projectId, weeks) =>
      client.request('management.metrics', weeks ? { projectId, weeks } : { projectId }, projectMetricsSchema),
    forecast: (projectId) => client.request('management.forecast', { projectId }, projectForecastSchema),
    risks: (projectId) => client.request('management.risks', { projectId }, projectRisksSchema),
    modelStatus: () => client.request('model.status', {}, modelStatusSchema),
    evidence: (projectId) => client.request('management.evidence', { projectId }, evidencePackSchema),
    ask: (projectId, question) => client.request('management.ask', { projectId, question }, assistantAnswerSchema)
  };
}
