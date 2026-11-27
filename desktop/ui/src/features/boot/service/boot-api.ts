import type { EngineClient } from '@/shared/api';
import { bootCapabilitiesSchema, bootListSchema, bootResponseSchema } from '@/shared/schema';

export type BootHint = 'request' | 'agent' | 'assistant' | 'idea';

export interface BootRequestInput {
  projectId?: string;
  text?: string;
  /** O tipo que a tela sugeriu pelo texto; reforça o reconhecimento, não decide. */
  hint?: BootHint;
  /** Quando a pessoa escolheu a atividade ou uma das opções que o Boot ofereceu: não há o que interpretar. */
  capabilityId?: string;
}

const SOURCE = 'home';

/** O Boot do motor: a interface só pede e mostra; interpretar, conferir permissão e executar é do motor. */
export function createBootApi(client: EngineClient) {
  return {
    start: async (input: BootRequestInput) =>
      (await client.request('boot.start', { ...input, source: SOURCE }, bootResponseSchema)).boot,
    project: async (projectId: string) =>
      (await client.request('boot.project', { projectId, source: SOURCE }, bootResponseSchema)).boot,
    resume: async (projectId: string, bootId: string) =>
      (await client.request('boot.resume', { projectId, bootId }, bootResponseSchema)).boot,
    detail: async (projectId: string, bootId: string) =>
      (await client.request('boot.detail', { projectId, bootId }, bootResponseSchema)).boot,
    list: (projectId?: string) => client.request('boot.list', projectId ? { projectId } : {}, bootListSchema),
    capabilities: (projectId?: string) =>
      client.request('boot.capabilities', projectId ? { projectId } : {}, bootCapabilitiesSchema)
  };
}

export type BootApi = ReturnType<typeof createBootApi>;
