import { z } from 'zod';

import { syncExecutionSchema } from './integration';

/** Indicador de contagem: ou há valor, ou há o motivo de não haver. */
const countMetricSchema = z.object({ value: z.number().nullable(), unavailable: z.string().nullable() });

const weekWindowSchema = z.object({ from: z.string(), to: z.string(), delivered: z.number(), created: z.number() });

export const kindFlowSchema = z.object({
  kind: z.string(),
  items: z.number(),
  open: z.number(),
  wip: countMetricSchema,
  throughput: z.object({
    weeks: z.array(weekWindowSchema),
    total: z.number().nullable(),
    weeklyAverage: z.number().nullable(),
    unavailable: z.string().nullable()
  }),
  leadTime: z.object({
    sample: z.number(),
    medianDays: z.number().nullable(),
    p85Days: z.number().nullable(),
    unavailable: z.string().nullable()
  }),
  /** Do início do trabalho (primeira entrada em andamento no histórico de status) à conclusão. */
  cycleTime: z.object({
    sample: z.number(),
    /** Itens entregues no período que ficaram de fora por não ter início conhecido. */
    excluded: z.number(),
    medianDays: z.number().nullable(),
    p85Days: z.number().nullable(),
    unavailable: z.string().nullable()
  }),
  /** Itens abertos bloqueados por rótulo ("blocked", "bloqueado") ou por vínculo de bloqueio do Jira. */
  blocked: z.object({
    value: z.number().nullable(),
    viaLink: z.number(),
    viaLabel: z.number(),
    /** Itens com os vínculos lidos: sem isso, "nenhum bloqueio por vínculo" não quer dizer nada. */
    linksRead: z.number(),
    /** O que foi lido, por extenso. */
    basis: z.string(),
    unavailable: z.string().nullable()
  }),
  overdue: z.object({ value: z.number().nullable(), withDueDate: z.number(), unavailable: z.string().nullable() })
});

/** Indicadores de um projeto, calculados pelo motor a partir do que foi sincronizado. */
export const projectMetricsSchema = z.object({
  projectId: z.string(),
  generatedAt: z.string(),
  periodWeeks: z.number(),
  periodFrom: z.string(),
  periodTo: z.string(),
  complete: z.boolean(),
  sources: z.array(
    z.object({
      integrationId: z.string(),
      provider: z.string(),
      resourceName: z.string(),
      lastSyncStatus: syncExecutionSchema.shape.status.nullable(),
      lastSyncedAt: z.string().nullable(),
      complete: z.boolean()
    })
  ),
  kinds: z.array(kindFlowSchema),
  notCalculated: z.array(z.object({ id: z.string(), label: z.string(), reason: z.string() }))
});

export const portfolioSchema = z.object({
  generatedAt: z.string(),
  periodWeeks: z.number(),
  projects: z.array(
    z.object({
      projectId: z.string(),
      displayName: z.string(),
      displayPath: z.string(),
      sources: z.number(),
      kinds: z.array(
        z.object({
          kind: z.string(),
          items: z.number(),
          open: z.number(),
          inProgress: z.number(),
          deliveredInPeriod: z.number(),
          overdue: z.number().nullable()
        })
      ),
      oldestReadAt: z.string().nullable(),
      signals: z.array(z.object({ id: z.string(), message: z.string() }))
    })
  )
});

export type KindFlow = z.infer<typeof kindFlowSchema>;
export type ProjectMetrics = z.infer<typeof projectMetricsSchema>;
export type Portfolio = z.infer<typeof portfolioSchema>;
export type PortfolioProject = Portfolio['projects'][number];

const forecastScenarioSchema = z.object({
  id: z.string(),
  label: z.string(),
  percentile: z.number(),
  weeks: z.number(),
  by: z.string()
});

export const confidenceSchema = z.enum(['low', 'medium', 'high']);

/** A previsão de um trabalho aberto (um tipo de dado ou um marco). Sem base, vem sem cenários e com o motivo. */
export const deliveryForecastSchema = z.object({
  kind: z.string(),
  remaining: z.number(),
  historyWeeks: z.number(),
  weeklyThroughput: z.array(z.number()),
  trials: z.number(),
  scenarios: z.array(forecastScenarioSchema),
  confidence: confidenceSchema.nullable(),
  confidenceReasons: z.array(z.string()),
  riskFactors: z.array(z.string()),
  unavailable: z.string().nullable()
});

/** Previsão de conclusão calculada pelo motor (Monte Carlo sobre o throughput). Sem base, vem sem cenários e com o motivo. */
export const projectForecastSchema = z.object({
  projectId: z.string(),
  generatedAt: z.string(),
  method: z.string(),
  complete: z.boolean(),
  assumptions: z.array(z.string()),
  limitations: z.array(z.string()),
  kinds: z.array(deliveryForecastSchema)
});

export const riskSeveritySchema = z.enum(['low', 'medium', 'high']);

/** Riscos detectados por regra fixa. A origem fica explícita para nunca misturar com sugestão de IA. */
export const projectRisksSchema = z.object({
  projectId: z.string(),
  generatedAt: z.string(),
  complete: z.boolean(),
  hasData: z.boolean(),
  risks: z.array(
    z.object({
      id: z.string(),
      title: z.string(),
      severity: riskSeveritySchema,
      rule: z.string(),
      evidence: z.array(z.string()),
      items: z.array(z.object({ kind: z.string(), externalId: z.string(), title: z.string() })),
      trend: z.string().nullable(),
      recommendation: z.string(),
      origin: z.string()
    })
  ),
  rulesEvaluated: z.array(z.string())
});

/** O andamento de um marco (milestone do GitHub ou versão de correção do Jira) dentro de um tipo de dado. */
export const milestoneProgressSchema = z.object({
  kind: z.string(),
  name: z.string(),
  dueDate: z.string().nullable(),
  items: z.number(),
  delivered: z.number(),
  /** Concluídos que não contam como entrega: descartados, duplicados, pull request fechado sem merge. */
  closedWithoutDelivery: z.number(),
  open: z.number(),
  /** done, on_track, at_risk, behind, overdue, no_forecast ou no_due_date. Um valor novo aparece como veio. */
  outlook: z.string(),
  outlookReason: z.string(),
  forecast: deliveryForecastSchema
});

/** Marcos de um projeto (`management.milestones`): andamento, prazo e previsão do que falta em cada um. */
export const projectMilestonesSchema = z.object({
  projectId: z.string(),
  generatedAt: z.string(),
  complete: z.boolean(),
  milestones: z.array(milestoneProgressSchema),
  /** Marcos que ficaram fora da lista por passar do limite. */
  omitted: z.number(),
  unassigned: z.array(z.object({ kind: z.string(), items: z.number(), open: z.number() })),
  assumptions: z.array(z.string()),
  limitations: z.array(z.string())
});

/** Uma mudança na trajetória de um risco. `kind`: observed, appeared, raised, lowered, items_changed ou resolved. */
export const riskChangeSchema = z.object({
  at: z.string(),
  kind: z.string(),
  from: z.string().nullable(),
  to: z.string().nullable(),
  itemsAdded: z.array(z.string()),
  itemsRemoved: z.array(z.string())
});

export const riskTrailSchema = z.object({
  id: z.string(),
  title: z.string(),
  active: z.boolean(),
  currentSeverity: z.string().nullable(),
  firstSeenAt: z.string(),
  resolvedAt: z.string().nullable(),
  changes: z.array(riskChangeSchema)
});

/** O histórico dos riscos (`management.riskhistory`): a trajetória de cada risco a partir dos registros do motor. */
export const projectRiskHistorySchema = z.object({
  projectId: z.string(),
  generatedAt: z.string(),
  /** Quando foi feito o primeiro registro; antes disso não há histórico. */
  since: z.string().nullable(),
  lastCheckedAt: z.string().nullable(),
  snapshots: z.number(),
  trails: z.array(riskTrailSchema),
  omitted: z.number(),
  basis: z.string()
});

/** Uma decisão registrada no período, sem os fatos do dia. */
export const reportDecisionSchema = z.object({
  id: z.string(),
  recordedAt: z.string(),
  title: z.string(),
  decision: z.string(),
  runs: z.number(),
  facts: z.number()
});

/** Uma mudança de risco que aconteceu dentro do período, com o risco a que pertence. */
export const reportRiskChangeSchema = z.object({ riskId: z.string(), title: z.string(), change: riskChangeSchema });

/** Quantas execuções de um tipo estão num estado, entre as que começaram no período. */
export const reportRunsSchema = z.object({ kind: z.string(), state: z.string(), count: z.number() });

/**
 * O relatório de um período (`management.report`): indicadores, previsão, riscos, marcos, decisões e execuções, todos do motor.
 * Nenhum texto é escrito por modelo.
 */
export const projectReportSchema = z.object({
  projectId: z.string(),
  projectName: z.string(),
  generatedAt: z.string(),
  weeks: z.number(),
  periodFrom: z.string(),
  periodTo: z.string(),
  metrics: projectMetricsSchema,
  forecast: projectForecastSchema,
  risks: projectRisksSchema,
  riskChanges: z.array(reportRiskChangeSchema),
  milestones: projectMilestonesSchema,
  decisions: z.array(reportDecisionSchema),
  runs: z.array(reportRunsSchema),
  basis: z.string()
});

export type ProjectReport = z.infer<typeof projectReportSchema>;
export type RiskChange = z.infer<typeof riskChangeSchema>;
export type RiskTrail = z.infer<typeof riskTrailSchema>;
export type ProjectRiskHistory = z.infer<typeof projectRiskHistorySchema>;
export type MilestoneProgress = z.infer<typeof milestoneProgressSchema>;
export type ProjectMilestones = z.infer<typeof projectMilestonesSchema>;
export type ProjectForecast = z.infer<typeof projectForecastSchema>;
export type DeliveryForecast = ProjectForecast['kinds'][number];
export type ProjectRisks = z.infer<typeof projectRisksSchema>;
export type ProjectRisk = ProjectRisks['risks'][number];

const evidenceFactSchema = z.object({ id: z.string(), group: z.string(), text: z.string() });

/** O que vai para o provedor de modelo numa pergunta, além da própria pergunta. */
export const evidencePackSchema = z.object({
  projectId: z.string(),
  projectName: z.string(),
  generatedAt: z.string(),
  facts: z.array(evidenceFactSchema),
  withheld: z.array(z.string())
});

const statementSchema = z.object({ text: z.string(), evidence: z.array(z.string()) });

/** A resposta do assistente, já conferida pelo motor contra o pacote de evidências. */
export const assistantAnswerSchema = z.object({
  projectId: z.string(),
  answeredAt: z.string(),
  summary: z.string(),
  observed: z.array(statementSchema),
  calculations: z.array(statementSchema),
  inferences: z.array(statementSchema),
  recommendations: z.array(statementSchema),
  unavailable: z.array(z.string()),
  rejected: z.array(z.object({ section: z.string(), text: z.string(), reason: z.string() })),
  evidence: z.array(evidenceFactSchema),
  model: z.string(),
  inputTokens: z.number(),
  outputTokens: z.number()
});

/** Uma etapa do caminho da investigação: só nomes de grupo e ids de fatos, nunca texto de fato, de hipótese nem a pergunta. */
export const investigationStepSchema = z.object({
  id: z.string(),
  title: z.string(),
  detail: z.string(),
  round: z.number(),
  groups: z.array(z.string()),
  facts: z.array(z.string())
});

export const investigationLimitationSchema = z.object({ code: z.string(), message: z.string() });

/**
 * A investigação em etapas (`management.investigate`): a resposta, conferida pelo motor só contra os fatos que a investigação de
 * fato consultou, mais o caminho percorrido. `runId` é a execução dela na central, onde o caminho pode ser reaberto.
 */
export const projectInvestigationSchema = z.object({
  investigationId: z.string(),
  runId: z.string(),
  answeredAt: z.string(),
  rounds: z.number(),
  usedModel: z.boolean(),
  answer: assistantAnswerSchema,
  path: z.array(investigationStepSchema),
  limitations: z.array(investigationLimitationSchema)
});

export type InvestigationStep = z.infer<typeof investigationStepSchema>;
export type ProjectInvestigation = z.infer<typeof projectInvestigationSchema>;
export type EvidenceFact = z.infer<typeof evidenceFactSchema>;
export type EvidencePack = z.infer<typeof evidencePackSchema>;
export type AssistantAnswer = z.infer<typeof assistantAnswerSchema>;
export type AssistantStatement = z.infer<typeof statementSchema>;
