import { z } from 'zod';

import { snapshotOf } from './run';

export const IDEA_STAGES = ['specification', 'architecture', 'backlog'] as const;
export type IdeaStage = (typeof IDEA_STAGES)[number];

export const ideaCheckSchema = z.object({
  id: z.string(),
  label: z.string(),
  passed: z.boolean(),
  detail: z.string().nullable()
});

/** O que o modelo escreveu para a etapa e o que o motor conferiu. `artifact` volta inteiro para a etapa seguinte. */
export const ideaDraftSchema = z.object({
  stage: z.enum(IDEA_STAGES),
  artifact: z.record(z.string(), z.unknown()).nullable(),
  checks: z.array(ideaCheckSchema),
  valid: z.boolean(),
  attempts: z.number(),
  inputTokens: z.number(),
  outputTokens: z.number()
});

export const ideaPlanSchema = z.object({
  planId: z.string(),
  projectId: z.string(),
  changes: z.array(
    z.object({
      path: z.string(),
      op: z.string(),
      diff: z.string(),
      truncated: z.boolean(),
      beforeHash: z.string().nullable(),
      afterHash: z.string()
    })
  )
});

export const ideaSaveResultSchema = z.object({
  runId: z.string(),
  kind: z.literal('idea'),
  outcome: z.string(),
  files: z.array(z.object({ path: z.string(), op: z.string(), status: z.string() })),
  error: z.string().nullable()
});

export const ideaSaveSnapshotSchema = snapshotOf(ideaSaveResultSchema);

const text = z.string().catch('');
const texts = z.array(z.string()).catch([]);

/** Leitura tolerante só para mostrar: quem decide se a etapa vale é o motor, pelas verificações. */
export const ideaSpecificationView = z.object({
  title: text,
  problem: text,
  users: texts,
  goals: texts,
  requirements: z.array(z.object({ id: text, text, priority: text })).catch([]),
  nonFunctional: z.array(z.object({ id: text, text })).catch([]),
  outOfScope: texts,
  openQuestions: texts
});

export const ideaArchitectureView = z.object({
  style: text,
  summary: text,
  components: z.array(z.object({ id: text, name: text, responsibility: text, requirements: texts })).catch([]),
  stack: z.array(z.object({ area: text, choice: text, reason: text })).catch([]),
  risks: z.array(z.object({ text, mitigation: text })).catch([]),
  decisions: z.array(z.object({ title: text, choice: text, reason: text })).catch([])
});

export const ideaBacklogView = z.object({
  epics: z
    .array(
      z.object({
        id: text,
        title: text,
        stories: z
          .array(z.object({ id: text, title: text, requirements: texts, acceptance: texts, estimate: text }))
          .catch([])
      })
    )
    .catch([])
});

export type IdeaCheck = z.infer<typeof ideaCheckSchema>;
export type IdeaDraft = z.infer<typeof ideaDraftSchema>;
export type IdeaPlan = z.infer<typeof ideaPlanSchema>;
export type IdeaSaveResult = z.infer<typeof ideaSaveResultSchema>;
export type IdeaArtifact = Record<string, unknown>;
