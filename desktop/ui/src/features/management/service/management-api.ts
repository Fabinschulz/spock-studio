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
  projectReportSchema,
  projectRiskHistorySchema,
  modelStatusSchema,
  portfolioSchema,
  projectInvestigationSchema,
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
  type ProjectReport,
  type ProjectRiskHistory,
  type ModelStatus,
  type Portfolio,
  type ProjectInvestigation,
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
  /** O relatório de um período (em semanas), montado pelo motor com o que ele já calcula e registra. */
  report(projectId: string, weeks: number): Promise<ProjectReport>;
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
  /** A pergunta investigada em etapas: o motor cruza riscos, execuções e mudanças e confere a resposta. Funciona sem chave de modelo, só com os sinais. */
  investigate(projectId: string, question: string): Promise<ProjectInvestigation>;
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
    report: (projectId, weeks) => client.request('management.report', { projectId, weeks }, projectReportSchema),
    risks: (projectId) => client.request('management.risks', { projectId }, projectRisksSchema),
    modelStatus: () => client.request('model.status', {}, modelStatusSchema),
    linkedRuns: (projectId) => client.request('run.linked', { projectId }, linkedItemsResultSchema),
    evidence: (projectId) => client.request('management.evidence', { projectId }, evidencePackSchema),
    ask: (projectId, question) => client.request('management.ask', { projectId, question }, assistantAnswerSchema),
    investigate: (projectId, question) =>
      client.request('management.investigate', { projectId, question }, projectInvestigationSchema)
  };
}
