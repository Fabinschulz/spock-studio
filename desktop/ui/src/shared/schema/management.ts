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

/** Previsão de conclusão calculada pelo motor (Monte Carlo sobre o throughput). Sem base, vem sem cenários e com o motivo. */
export const projectForecastSchema = z.object({
  projectId: z.string(),
  generatedAt: z.string(),
  method: z.string(),
  complete: z.boolean(),
  assumptions: z.array(z.string()),
  limitations: z.array(z.string()),
  kinds: z.array(
    z.object({
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
    })
  )
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

export type ProjectForecast = z.infer<typeof projectForecastSchema>;
export type DeliveryForecast = ProjectForecast['kinds'][number];
export type ProjectRisks = z.infer<typeof projectRisksSchema>;
export type ProjectRisk = ProjectRisks['risks'][number];
