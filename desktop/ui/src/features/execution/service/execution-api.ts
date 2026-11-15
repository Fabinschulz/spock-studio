import { runUntilDone, type EngineClient } from '@/shared/api';
import { runDetailSchema, runStartedSchema } from '@/shared/schema';

import { workflowSnapshotSchema, type WorkflowSnapshot } from '../domain/run-timeline';

/** Com `sinceSeq`, o retrato traz só os eventos depois desse número. */
export function readSnapshot(client: EngineClient, runId: string, sinceSeq?: number): Promise<WorkflowSnapshot> {
  return client.request(
    'run.snapshot',
    sinceSeq === undefined ? { runId } : { runId, sinceSeq },
    workflowSnapshotSchema
  );
}

export type ResumeDecision = { approvalId: string; actionIds: string[] };

/** Ações só são executadas se vierem aqui, com o `approvalId` da aprovação que o motor abriu. */
export function resumeWorkflow(
  client: EngineClient,
  projectId: string,
  runId: string,
  decision?: ResumeDecision
): Promise<{ runId: string }> {
  return client.request(
    'workflow.resume',
    decision && decision.actionIds.length > 0 ? { projectId, runId, ...decision } : { projectId, runId },
    runStartedSchema
  );
}

/** O que o diário diz sobre desfazer esta execução; o motor decide, a tela só mostra. */
export async function readUndoState(
  client: EngineClient,
  projectId: string,
  runId: string
): Promise<{ canRevert: boolean; reverted: boolean }> {
  const detail = await client.request('run.detail', { projectId, runId }, runDetailSchema);
  return { canRevert: detail.canRevert, reverted: detail.reverted };
}

/** Mesmo caminho do Harness: o arquivo só sai se ainda estiver como a ação o deixou. */
export async function revertRun(client: EngineClient, projectId: string, runId: string): Promise<void> {
  await runUntilDone(
    client,
    async () => (await client.request('harness.revert', { projectId, runId }, runStartedSchema)).runId
  );
}
