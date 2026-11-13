import { z } from 'zod';

/**
 * Contrato das decisões gerenciais (`decision.record`, `decision.list` e `decision.get`). Uma decisão guarda o que foi decidido
 * e por quê, os fatos que o motor tinha no instante do registro e as execuções relacionadas. Não se edita nem se apaga.
 */

/** Uma execução ligada à decisão, como era quando a decisão foi registrada. */
export const decisionRunSchema = z.object({
  runId: z.string(),
  kind: z.string(),
  state: z.string(),
  startedAt: z.string()
});

/** Um fato do pacote de evidências no instante da decisão, com o texto que o motor escreveu. */
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

/** A decisão na lista: sem os fatos, só quantos são; eles vêm em `decision.get`. */
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
