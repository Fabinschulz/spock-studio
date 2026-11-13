import { z } from 'zod';

/**
 * `level`: ok, attention, problem ou info. `refType` e `refId` dizem para onde ver mais (`run` ou `decision`) e são
 * nulos quando não há destino.
 */
export const projectTimelineEventSchema = z.object({
  id: z.string(),
  at: z.string(),
  kind: z.string(),
  level: z.string(),
  title: z.string(),
  detail: z.string(),
  refType: z.string().nullable(),
  refId: z.string().nullable()
});

/** `available` é falso quando a fonte falhou. */
export const projectTimelineSourceSchema = z.object({
  kind: z.string(),
  label: z.string(),
  count: z.number(),
  available: z.boolean()
});

/** `nextBefore` é o corte da página seguinte; nulo no fim. */
export const projectTimelineSchema = z.object({
  projectId: z.string(),
  generatedAt: z.string(),
  events: z.array(projectTimelineEventSchema),
  nextBefore: z.string().nullable(),
  sources: z.array(projectTimelineSourceSchema)
});

export type ProjectTimelineEvent = z.infer<typeof projectTimelineEventSchema>;
export type ProjectTimelineSource = z.infer<typeof projectTimelineSourceSchema>;
export type ProjectTimeline = z.infer<typeof projectTimelineSchema>;
