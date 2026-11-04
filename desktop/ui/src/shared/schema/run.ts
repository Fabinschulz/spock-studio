import { z } from 'zod';

/**
 * O que vale para qualquer execução do motor (aplicação, validação, reversão ou workflow): eventos, retrato e estados
 * terminais. Cada jornada encaixa o próprio resultado no retrato com {@link snapshotOf}.
 */

export const runOutcomeSchema = z.enum([
  'completed',
  'completed_with_limitations',
  'failed',
  'requires_intervention',
  'cancelled'
]);

/** Estados em que a execução não muda mais: fonte única para quem espera ou acompanha uma execução. */
export const TERMINAL_STATES: readonly string[] = runOutcomeSchema.options;

export function isTerminalState(state: string): boolean {
  return TERMINAL_STATES.includes(state);
}

export const runEventSchema = z.object({
  v: z.literal(1),
  kind: z.literal('event'),
  runId: z.string(),
  type: z.string(),
  seq: z.number(),
  data: z.record(z.string(), z.unknown())
});

export const timelineEventSchema = z.object({
  seq: z.number(),
  type: z.string(),
  data: z.record(z.string(), z.unknown())
});

const snapshotBase = z.object({
  runId: z.string(),
  projectId: z.string(),
  kind: z.string(),
  state: z.string(),
  lastSeq: z.number(),
  events: z.array(timelineEventSchema)
});

/** Retrato de uma execução (`run.snapshot`) com o resultado da jornada que a iniciou. */
export function snapshotOf<T extends z.ZodType>(result: T) {
  return snapshotBase.extend({ result: result.nullable() });
}

export const runStartedSchema = z.object({ runId: z.string() });
export const cancelSchema = z.object({ cancelled: z.boolean() });

export type RunEvent = z.infer<typeof runEventSchema>;
export type TimelineEvent = z.infer<typeof timelineEventSchema>;
