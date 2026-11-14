import type { EngineClient } from '@/shared/api';
import { modelConnectionSchema, modelStatusSchema, type ModelConnection, type ModelStatus } from '@/shared/schema';

/** A chave vai para o motor e nunca volta. */
export type ModelApi = {
  status(): Promise<ModelStatus>;
  connection(): Promise<ModelConnection>;
  /** Guarda a chave deste provedor só depois de ele aceitá-la; se recusar, nada é gravado. Não mexe nas chaves dos outros. */
  save(provider: string, key: string): Promise<ModelStatus>;
  /** Uma recusa vem no estado, não como falha. */
  test(provider: string): Promise<ModelConnection>;
  /** Só entre os modelos de provedores com chave; nulo volta ao padrão. */
  setPurpose(purpose: string, model: string | null): Promise<ModelConnection>;
  remove(provider: string): Promise<ModelConnection>;
  setDefault(provider: string): Promise<ModelConnection>;
};

export function createModelApi(client: EngineClient): ModelApi {
  return {
    status: () => client.request('model.status', {}, modelStatusSchema),
    connection: () => client.request('model.connection', {}, modelConnectionSchema),
    save: (provider, key) => client.request('model.save', { provider, key }, modelStatusSchema),
    test: (provider) => client.request('model.test', { provider }, modelConnectionSchema),
    setPurpose: (purpose, model) =>
      client.request('model.purpose', model === null ? { purpose } : { purpose, model }, modelConnectionSchema),
    remove: (provider) => client.request('model.remove', { provider }, modelConnectionSchema),
    setDefault: (provider) => client.request('model.default', { provider }, modelConnectionSchema)
  };
}
