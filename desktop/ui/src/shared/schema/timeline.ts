import { z } from 'zod';

/**
 * Um acontecimento do projeto (`timeline.read`). `level`: ok, attention, problem ou info. `refType` e `refId` dizem para onde
 * ver mais (`run` ou `decision`) e são nulos quando não há destino. O texto é do motor.
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

/** Uma fonte da linha do tempo e quantos acontecimentos ela trouxe. `available` é falso quando a fonte falhou. */
export const projectTimelineSourceSchema = z.object({
  kind: z.string(),
  label: z.string(),
  count: z.number(),
  available: z.boolean()
});

/** A linha do tempo do projeto, do mais recente ao mais antigo. `nextBefore` é o corte da página seguinte; nulo no fim. */
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
