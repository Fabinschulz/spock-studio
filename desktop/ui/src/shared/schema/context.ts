import { z } from 'zod';

const sectionSchema = z.object({ id: z.string(), label: z.string(), tokens: z.number() });

/**
 * O pedido de engenharia como ele iria ao modelo, contado antes de gastar. `counting.exact` só é verdadeiro quando o
 * tokenizador é o do próprio modelo; o resto é estimativa. Só caminhos e números: o texto do projeto não vem.
 */
export const contextReviewSchema = z.object({
  projectId: z.string(),
  model: z.object({ configured: z.boolean(), provider: z.string().nullable(), model: z.string().nullable() }),
  counting: z.object({ exact: z.boolean(), method: z.string() }),
  budget: z.number(),
  plannedTokens: z.number(),
  calls: z.array(
    z.object({
      agentId: z.string(),
      label: z.string(),
      inputTokens: z.number(),
      outputTokens: z.number(),
      withinBudget: z.boolean(),
      sections: z.array(sectionSchema)
    })
  ),
  evidence: z.array(z.object({ path: z.string(), tokens: z.number(), included: z.boolean(), truncated: z.boolean() })),
  leftOut: z.object({
    cutBySize: z.number(),
    truncated: z.number(),
    pending: z.number(),
    excluded: z.array(
      z.object({ reason: z.string(), label: z.string(), count: z.number(), examples: z.array(z.string()) })
    )
  })
});

export type ContextReview = z.infer<typeof contextReviewSchema>;
