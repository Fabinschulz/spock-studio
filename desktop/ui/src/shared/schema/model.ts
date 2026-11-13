import { z } from 'zod';

export const modelStatusSchema = z.object({
  configured: z.boolean(),
  provider: z.string()
});

/** O que a última conferência da chave com o provedor encontrou. */
const modelCheckErrorSchema = z.object({ code: z.string(), message: z.string(), at: z.string() });

/**
 * O provedor de modelo como uma conexão (`model.connection`, `model.test`). `status`: none, untested, connected ou error.
 * `protection`: vault (cofre do sistema) ou file (arquivo neste computador); nulo sem chave. Nunca traz a chave.
 */
export const modelConnectionSchema = z.object({
  configured: z.boolean(),
  provider: z.string(),
  model: z.string(),
  status: z.string(),
  protection: z.string().nullable(),
  checkedAt: z.string().nullable(),
  lastError: modelCheckErrorSchema.nullable()
});

export type ModelStatus = z.infer<typeof modelStatusSchema>;
export type ModelConnection = z.infer<typeof modelConnectionSchema>;
