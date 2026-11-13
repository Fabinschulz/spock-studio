import type { EngineClient } from '@/shared/api';
import {
  assistantAnswerSchema,
  decisionGetSchema,
  decisionListSchema,
  decisionRecordResultSchema,
  evidencePackSchema,
  linkedItemsResultSchema,
  runListSchema,
  projectMilestonesSchema,
  projectRiskHistorySchema,
  modelStatusSchema,
  portfolioSchema,
  projectForecastSchema,
  projectMetricsSchema,
  projectRisksSchema,
  type AssistantAnswer,
  type DecisionSummary,
  type EvidencePack,
  type LinkedItemsResult,
  type ManagementDecision,
  type RunItem,
  type ProjectMilestones,
  type ProjectRiskHistory,
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
  /** As decisões gerenciais registradas no projeto, da mais recente para a mais antiga, sem os fatos do dia. */
  decisions(projectId: string): Promise<DecisionSummary[]>;
  /** Uma decisão com os fatos que o motor tinha no dia em que ela foi registrada. */
  decision(projectId: string, decisionId: string): Promise<ManagementDecision>;
  /** Registra a decisão. O motor guarda com ela os fatos de agora; `redacted` diz se algum trecho foi substituído por parecer um segredo. */
  recordDecision(input: {
    projectId: string;
    title: string;
    decision: string;
    rationale: string;
    runIds: string[];
  }): Promise<{ decision: ManagementDecision; redacted: boolean }>;
  /** As execuções mais recentes do projeto, para escolher as relacionadas a uma decisão. */
  recentRuns(projectId: string): Promise<RunItem[]>;
  /** A trajetória de cada risco, a partir dos registros que o motor faz a cada sincronização completa. */
  riskHistory(projectId: string): Promise<ProjectRiskHistory>;
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
    riskHistory: (projectId) => client.request('management.riskhistory', { projectId }, projectRiskHistorySchema),
    decisions: (projectId) =>
      client.request('decision.list', { projectId }, decisionListSchema).then((result) => result.decisions),
    decision: (projectId, decisionId) =>
      client.request('decision.get', { projectId, decisionId }, decisionGetSchema).then((result) => result.decision),
    recordDecision: ({ rationale, ...input }) =>
      client.request(
        'decision.record',
        { ...input, ...(rationale.trim() === '' ? {} : { rationale }) },
        decisionRecordResultSchema
      ),
    recentRuns: (projectId) =>
      client.request('run.list', { projectId, limit: 10 }, runListSchema).then((result) => result.runs),
    risks: (projectId) => client.request('management.risks', { projectId }, projectRisksSchema),
    modelStatus: () => client.request('model.status', {}, modelStatusSchema),
    linkedRuns: (projectId) => client.request('run.linked', { projectId }, linkedItemsResultSchema),
    evidence: (projectId) => client.request('management.evidence', { projectId }, evidencePackSchema),
    ask: (projectId, question) => client.request('management.ask', { projectId, question }, assistantAnswerSchema)
  };
}
