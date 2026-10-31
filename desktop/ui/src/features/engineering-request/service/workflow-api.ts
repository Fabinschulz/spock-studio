import type { EngineClient } from '@/shared/api';
import { workflowStartResultSchema, type WorkflowStartInput } from '@/shared/schema';

export type StartWorkflowInput = {
  projectId: string;
  input: WorkflowStartInput;
};

export function startWorkflow(client: EngineClient, { projectId, input }: StartWorkflowInput) {
  return client.request('workflow.start', { projectId, input }, workflowStartResultSchema);
}
