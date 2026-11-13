import { z } from 'zod';

/** Uma decisão guarda os fatos que o motor tinha no instante do registro; não se edita nem se apaga. */

/** Uma execução ligada à decisão, como era quando a decisão foi registrada. */
export const decisionRunSchema = z.object({
  runId: z.string(),
  kind: z.string(),
  state: z.string(),
  startedAt: z.string()
});

export const decisionFactSchema = z.object({ id: z.string(), group: z.string(), text: z.string() });

export const managementDecisionSchema = z.object({
  id: z.string(),
  projectId: z.string(),
  recordedAt: z.string(),
  title: z.string(),
  decision: z.string(),
  rationale: z.string().nullable(),
  runs: z.array(decisionRunSchema),
  facts: z.array(decisionFactSchema),
  /** O que ficou de fora do pacote de fatos de propósito. */
  withheld: z.array(z.string())
});

/** Sem os fatos, só quantos são; eles vêm em `decision.get`. */
export const decisionSummarySchema = z.object({
  id: z.string(),
  recordedAt: z.string(),
  title: z.string(),
  decision: z.string(),
  rationale: z.string().nullable(),
  runs: z.array(decisionRunSchema),
  factCount: z.number()
});

export const decisionListSchema = z.object({ decisions: z.array(decisionSummarySchema) });

/** `redacted` é verdadeiro quando algum trecho parecia um segredo e foi substituído antes de guardar. */
export const decisionRecordResultSchema = z.object({ decision: managementDecisionSchema, redacted: z.boolean() });

export const decisionGetSchema = z.object({ decision: managementDecisionSchema });

export const DECISION_LIMITS = { title: 120, decision: 1000, rationale: 2000, runs: 10 } as const;

export type DecisionRun = z.infer<typeof decisionRunSchema>;
export type ManagementDecision = z.infer<typeof managementDecisionSchema>;
export type DecisionSummary = z.infer<typeof decisionSummarySchema>;
