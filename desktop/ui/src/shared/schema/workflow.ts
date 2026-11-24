import { z } from 'zod';

import { runStartedSchema, snapshotOf } from './run';

/** Stacks vêm do contexto do motor. */
export const workflowStartInputSchema = z.object({
  goal: z.string().min(1),
  impact: z.boolean().optional(),
  requiresApproval: z.boolean().optional(),
  /** O motor recusa se não houver modelo configurado. */
  useModel: z.boolean().optional()
});

/** Parâmetros de workflow.start; espelha contracts/schemas/ai/workflow.start.params.schema.json */
export const workflowStartParamsSchema = z
  .object({
    projectId: z.string().min(1),
    input: workflowStartInputSchema.optional()
  })
  .strict();

export const workflowStartResultSchema = runStartedSchema;

export type WorkflowStartInput = z.infer<typeof workflowStartInputSchema>;

const evidenceSchema = z.object({
  filePath: z.string(),
  excerpt: z.string(),
  chunkId: z.string(),
  hash: z.string().optional(),
  score: z.number().optional()
});

const failureSchema = z.object({ code: z.string(), message: z.string() });

const impactSchema = z.object({
  schema: z.literal('engineering.impact.v1'),
  areas: z.array(z.string()),
  risks: z.array(z.string()),
  evidence: z.array(z.string())
});

/** A opinião do juiz por modelo (EV-JUDGE-001) não é veredito. */
const judgmentSchema = z.object({
  criteriaVersion: z.string(),
  scores: z.record(z.string(), z.number()),
  rationale: z.string(),
  evidence: z.array(z.string()).default([]),
  model: z.string(),
  modelVersion: z.string()
});

const evaluationSchema = z.object({
  verdict: z.literal('deterministic'),
  judge: z.enum(['not_run', 'passed', 'failed']),
  tokens: z.number(),
  judgment: judgmentSchema.optional()
});

/** O resultado de workflow.start no run.snapshot (UI-24). A fixture golden workflow-snapshot.json sai de uma execução real. */
export const workflowResultSchema = z.object({
  kind: z.literal('workflow'),
  // Uma execução que falhou não tem resumo: o resultado traz só `error`. Exigir o resumo fazia a página recusar o retrato inteiro.
  summary: z.string().default(''),
  error: failureSchema.optional(),
  evidence: z.array(evidenceSchema).default([]),
  limitations: z.array(z.object({ code: z.string(), message: z.string() })).default([]),
  impact: impactSchema.optional(),
  evaluation: evaluationSchema.optional(),
  actions: z
    .array(
      z.object({
        id: z.string(),
        kind: z.string(),
        summary: z.string(),
        target: z.string().optional(),
        status: z.string()
      })
    )
    .optional(),
  /** Só vem em execuções que usaram modelo. */
  usage: z.object({ modelCalls: z.number(), inputTokens: z.number(), outputTokens: z.number() }).optional()
});

export const workflowSnapshotSchema = snapshotOf(workflowResultSchema);

export type WorkflowSnapshot = z.infer<typeof workflowSnapshotSchema>;
