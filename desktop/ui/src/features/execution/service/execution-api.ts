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

/** A decisão de quem aprova: o identificador da aprovação que viu e as ações que aprova (nenhuma, se não marcar). */
export type ResumeDecision = { approvalId: string; actionIds: string[] };

/**
 * Aprova a pausa: o motor confere a aprovação pendente e retoma a execução. Sem decisão, nenhuma ação é aprovada; ações só
 * são executadas se vierem aqui, com o `approvalId` da aprovação que o motor abriu.
 */
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
