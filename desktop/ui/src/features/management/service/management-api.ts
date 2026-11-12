import type { EngineClient } from '@/shared/api';
import {
  assistantAnswerSchema,
  evidencePackSchema,
  linkedItemsResultSchema,
  projectMilestonesSchema,
  modelStatusSchema,
  portfolioSchema,
  projectForecastSchema,
  projectMetricsSchema,
  projectRisksSchema,
  type AssistantAnswer,
  type EvidencePack,
  type LinkedItemsResult,
  type ProjectMilestones,
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
  /** Andamento, prazo e previsão do que falta em cada marco (milestone do GitHub, versão do Jira). */
  milestones(projectId: string): Promise<ProjectMilestones>;
  modelStatus(): Promise<ModelStatus>;
  evidence(projectId: string): Promise<EvidencePack>;
  /** Os itens do projeto que têm execuções ligadas. Só leitura; as ligações se fazem no detalhe da execução. */
  linkedRuns(projectId: string): Promise<LinkedItemsResult>;
  ask(projectId: string, question: string): Promise<AssistantAnswer>;
};

export function createManagementApi(client: EngineClient): ManagementApi {
  return {
    portfolio: () => client.request('management.portfolio', {}, portfolioSchema),
    metrics: (projectId, weeks) =>
      client.request('management.metrics', weeks ? { projectId, weeks } : { projectId }, projectMetricsSchema),
    forecast: (projectId) => client.request('management.forecast', { projectId }, projectForecastSchema),
    milestones: (projectId) => client.request('management.milestones', { projectId }, projectMilestonesSchema),
    risks: (projectId) => client.request('management.risks', { projectId }, projectRisksSchema),
    modelStatus: () => client.request('model.status', {}, modelStatusSchema),
    linkedRuns: (projectId) => client.request('run.linked', { projectId }, linkedItemsResultSchema),
    evidence: (projectId) => client.request('management.evidence', { projectId }, evidencePackSchema),
    ask: (projectId, question) => client.request('management.ask', { projectId, question }, assistantAnswerSchema)
  };
}
