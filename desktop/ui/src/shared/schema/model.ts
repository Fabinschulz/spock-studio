import { z } from 'zod';

export const modelStatusSchema = z.object({
  configured: z.boolean(),
  provider: z.string()
});

const modelCheckErrorSchema = z.object({ code: z.string(), message: z.string(), at: z.string() });

export const modelOptionSchema = z.object({ id: z.string(), name: z.string() });

/**
 * Um provedor e o estado da chave dele. `status`: none, untested, connected ou error. `protection`: vault (cofre do
 * sistema) ou file (arquivo neste computador); nulo sem chave. `model` é o modelo de toda finalidade em que a pessoa não
 * escolheu outro, quando este é o provedor padrão. Nunca traz a chave.
 */
export const providerConnectionSchema = z.object({
  id: z.string(),
  name: z.string(),
  configured: z.boolean(),
  isDefault: z.boolean(),
  status: z.string(),
  protection: z.string().nullable(),
  checkedAt: z.string().nullable(),
  lastError: modelCheckErrorSchema.nullable(),
  model: z.string(),
  models: z.array(modelOptionSchema)
});

/** O modelo de uma finalidade hoje: o escolhido ou, sem escolha, o padrão do provedor padrão (`isDefault`). */
export const modelPurposeSchema = z.object({
  id: z.string(),
  label: z.string(),
  description: z.string(),
  /** O provedor sai do modelo: um modelo é de um provedor só. */
  provider: z.string(),
  model: z.string(),
  isDefault: z.boolean()
});

/** `provider` é o padrão; vazio sem nenhuma chave. Todos os provedores vêm na lista, com ou sem chave. */
export const modelConnectionSchema = z.object({
  configured: z.boolean(),
  provider: z.string(),
  providers: z.array(providerConnectionSchema),
  /** Uma por finalidade (assistente, juiz, fluxos); vazio sem chave. */
  purposes: z.array(modelPurposeSchema)
});

export type ModelStatus = z.infer<typeof modelStatusSchema>;
export type ModelConnection = z.infer<typeof modelConnectionSchema>;
export type ProviderConnection = z.infer<typeof providerConnectionSchema>;
export type ModelPurpose = z.infer<typeof modelPurposeSchema>;
