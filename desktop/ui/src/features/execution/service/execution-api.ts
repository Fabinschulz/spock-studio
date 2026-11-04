import type { EngineClient } from '@/shared/api';
import { runStartedSchema } from '@/shared/schema';

import { workflowSnapshotSchema, type WorkflowSnapshot } from '../domain/run-timeline';

/** O que a página de execução pede ao motor. Com `sinceSeq`, o retrato traz só os eventos depois desse número. */
export function readSnapshot(client: EngineClient, runId: string, sinceSeq?: number): Promise<WorkflowSnapshot> {
  return client.request(
    'run.snapshot',
    sinceSeq === undefined ? { runId } : { runId, sinceSeq },
    workflowSnapshotSchema
  );
}

/** Aprova a pausa: o motor confere a aprovação pendente e retoma a execução. */
export function resumeWorkflow(client: EngineClient, projectId: string, runId: string): Promise<{ runId: string }> {
  return client.request('workflow.resume', { projectId, runId }, runStartedSchema);
}
