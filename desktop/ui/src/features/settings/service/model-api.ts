import type { EngineClient } from '@/shared/api';
import { modelStatusSchema, type ModelStatus } from '@/shared/schema';

/** Chave do modelo, como o motor expõe. A chave vai para o motor e nunca volta. */
export type ModelApi = {
  status(): Promise<ModelStatus>;
  save(key: string): Promise<ModelStatus>;
  clear(): Promise<ModelStatus>;
};

export function createModelApi(client: EngineClient): ModelApi {
  return {
    status: () => client.request('model.status', {}, modelStatusSchema),
    save: (key) => client.request('model.save', { provider: 'openai', key }, modelStatusSchema),
    clear: () => client.request('model.clear', {}, modelStatusSchema)
  };
}
