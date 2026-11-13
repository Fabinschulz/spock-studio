import type { EngineClient } from '@/shared/api';
import { modelConnectionSchema, modelStatusSchema, type ModelConnection, type ModelStatus } from '@/shared/schema';

/** Chave do modelo, como o motor expõe. A chave vai para o motor e nunca volta. */
export type ModelApi = {
  status(): Promise<ModelStatus>;
  /** O provedor como uma conexão: se há chave, onde está guardada e o que a última conferência encontrou. */
  connection(): Promise<ModelConnection>;
  /** Guarda a chave só depois de o provedor aceitá-la; se ele recusar, nada é gravado. */
  save(key: string): Promise<ModelStatus>;
  /** Confere a chave guardada com o provedor. Uma recusa vem no estado, não como falha. */
  test(): Promise<ModelConnection>;
  clear(): Promise<ModelStatus>;
};

export function createModelApi(client: EngineClient): ModelApi {
  return {
    status: () => client.request('model.status', {}, modelStatusSchema),
    connection: () => client.request('model.connection', {}, modelConnectionSchema),
    save: (key) => client.request('model.save', { provider: 'openai', key }, modelStatusSchema),
    test: () => client.request('model.test', {}, modelConnectionSchema),
    clear: () => client.request('model.clear', {}, modelStatusSchema)
  };
}
