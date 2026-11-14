import type { EngineClient } from '@/shared/api';
import { aiToolListSchema, type AiTool } from '@/shared/schema';

export type AiToolsApi = {
  list(): Promise<AiTool[]>;
};

export function createAiToolsApi(client: EngineClient): AiToolsApi {
  return {
    list: async () => (await client.request('aitool.list', {}, aiToolListSchema)).tools
  };
}
