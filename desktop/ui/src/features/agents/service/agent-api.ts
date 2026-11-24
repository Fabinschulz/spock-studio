import type { EngineClient } from '@/shared/api';
import {
  agentSnapshotSchema,
  aiToolListSchema,
  cancelSchema,
  ideConnectionSchema,
  runStartedSchema,
  type AiTool,
  type IdeConnection,
  type RunEvent
} from '@/shared/schema';
import type { z } from 'zod';

export type AgentSnapshot = z.infer<typeof agentSnapshotSchema>;

export interface AgentApi {
  tools(): Promise<AiTool[]>;
  start(projectId: string, tool: string, prompt: string): Promise<string>;
  snapshot(runId: string, sinceSeq?: number): Promise<AgentSnapshot>;
  cancel(runId: string): Promise<void>;
  onEvent(handler: (event: RunEvent) => void): Promise<() => void>;
  ideConnection(): Promise<IdeConnection>;
}

export function createAgentApi(client: EngineClient): AgentApi {
  return {
    tools: async () => (await client.request('aitool.list', {}, aiToolListSchema)).tools,
    start: async (projectId, tool, prompt) =>
      (await client.request('agent.run', { projectId, tool, prompt }, runStartedSchema)).runId,
    snapshot: (runId, sinceSeq) =>
      client.request('run.snapshot', sinceSeq === undefined ? { runId } : { runId, sinceSeq }, agentSnapshotSchema),
    cancel: async (runId) => {
      await client.request('run.cancel', { runId }, cancelSchema);
    },
    onEvent: (handler) => client.onEvent(handler),
    ideConnection: () => client.request('ide.connection', {}, ideConnectionSchema)
  };
}
