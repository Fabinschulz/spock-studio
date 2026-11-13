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
/** Um provedor que esta versão sabe chamar, com o modelo que as conversas dos agentes usam nele. */
/** Um modelo que a pessoa pode escolher num provedor. */
export const modelOptionSchema = z.object({ id: z.string(), name: z.string() });

export const modelProviderSchema = z.object({
  id: z.string(),
  name: z.string(),
  /** O modelo de toda finalidade em que a pessoa não escolheu outro. */
  model: z.string(),
  models: z.array(modelOptionSchema)
});

/** O modelo de uma finalidade hoje: o escolhido ou, sem escolha, o padrão do provedor (`isDefault`). */
export const modelPurposeSchema = z.object({
  id: z.string(),
  label: z.string(),
  description: z.string(),
  model: z.string(),
  isDefault: z.boolean()
});

export const modelConnectionSchema = z.object({
  configured: z.boolean(),
  provider: z.string(),
  providerName: z.string(),
  model: z.string(),
  status: z.string(),
  protection: z.string().nullable(),
  checkedAt: z.string().nullable(),
  lastError: modelCheckErrorSchema.nullable(),
  available: z.array(modelProviderSchema),
  /** Uma por finalidade (assistente, juiz, fluxos); vazio sem chave. */
  purposes: z.array(modelPurposeSchema)
});

export type ModelStatus = z.infer<typeof modelStatusSchema>;
export type ModelConnection = z.infer<typeof modelConnectionSchema>;
export type ModelProvider = z.infer<typeof modelProviderSchema>;
export type ModelPurpose = z.infer<typeof modelPurposeSchema>;
