import type { EngineClient } from '@/shared/api';
import { modelConnectionSchema, modelStatusSchema, type ModelConnection, type ModelStatus } from '@/shared/schema';

/** A chave vai para o motor e nunca volta. */
export type ModelApi = {
  status(): Promise<ModelStatus>;
  connection(): Promise<ModelConnection>;
  /** Guarda a chave só depois de o provedor aceitá-la; se ele recusar, nada é gravado. */
  save(provider: string, key: string): Promise<ModelStatus>;
  /** Uma recusa vem no estado, não como falha. */
  test(): Promise<ModelConnection>;
  /** Só entre os modelos do provedor da chave; nulo volta ao padrão. */
  setPurpose(purpose: string, model: string | null): Promise<ModelConnection>;
  clear(): Promise<ModelStatus>;
};

export function createModelApi(client: EngineClient): ModelApi {
  return {
    status: () => client.request('model.status', {}, modelStatusSchema),
    connection: () => client.request('model.connection', {}, modelConnectionSchema),
    save: (provider, key) => client.request('model.save', { provider, key }, modelStatusSchema),
    test: () => client.request('model.test', {}, modelConnectionSchema),
    setPurpose: (purpose, model) =>
      client.request('model.purpose', model === null ? { purpose } : { purpose, model }, modelConnectionSchema),
    clear: () => client.request('model.clear', {}, modelStatusSchema)
  };
}
