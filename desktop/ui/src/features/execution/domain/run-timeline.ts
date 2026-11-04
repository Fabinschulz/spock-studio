import { z } from 'zod';

import { isTerminalState, snapshotOf, type TimelineEvent } from '@/shared/schema';

const evidenceSchema = z.object({
  filePath: z.string(),
  excerpt: z.string(),
  chunkId: z.string()
});

const impactSchema = z.object({
  schema: z.literal('engineering.impact.v1'),
  areas: z.array(z.string()),
  risks: z.array(z.string()),
  evidence: z.array(z.string())
});

const workflowResultSchema = z.object({
  kind: z.literal('workflow'),
  summary: z.string(),
  evidence: z.array(evidenceSchema).default([]),
  limitations: z.array(z.object({ code: z.string(), message: z.string() })).default([]),
  impact: impactSchema.optional()
});

export const workflowSnapshotSchema = snapshotOf(workflowResultSchema);

export type WorkflowSnapshot = z.infer<typeof workflowSnapshotSchema>;
export type { TimelineEvent };

/** O que a página sabe da execução: o último retrato do motor mais os eventos que chegaram depois, sem buraco nem repetição. */
export type RunView = {
  snapshot: WorkflowSnapshot;
  events: TimelineEvent[];
  lastSeq: number;
};

export const isTerminal = isTerminalState;

export function fromSnapshot(snapshot: WorkflowSnapshot, previous?: RunView): RunView {
  const events = mergeEvents(previous?.events ?? [], snapshot.events);
  const lastSeq = Math.max(previous?.lastSeq ?? -1, snapshot.lastSeq, ...events.map((event) => event.seq));
  return { snapshot, events, lastSeq: Number.isFinite(lastSeq) ? lastSeq : -1 };
}

export type LiveOutcome = { view: RunView; gap: boolean };

/**
 * Um evento ao vivo entra só se for o próximo número. Repetido ou atrasado é ignorado; se pular números, a página pede
 * ao motor o que faltou (`run.snapshot` com `sinceSeq`) em vez de mostrar uma sequência com buraco.
 */
export function applyLive(view: RunView, event: TimelineEvent): LiveOutcome {
  if (event.seq <= view.lastSeq) return { view, gap: false };
  if (event.seq > view.lastSeq + 1) return { view, gap: true };
  const state = stateFrom(event) ?? view.snapshot.state;
  return {
    view: { snapshot: { ...view.snapshot, state }, events: [...view.events, event], lastSeq: event.seq },
    gap: false
  };
}

function stateFrom(event: TimelineEvent): string | null {
  if (event.type !== 'run.state' && event.type !== 'run.completed') return null;
  return typeof event.data.state === 'string' ? event.data.state : null;
}

function mergeEvents(current: TimelineEvent[], incoming: TimelineEvent[]): TimelineEvent[] {
  const bySeq = new Map<number, TimelineEvent>();
  for (const event of [...current, ...incoming]) bySeq.set(event.seq, event);
  return [...bySeq.values()].sort((a, b) => a.seq - b.seq);
}

const AGENT_LABEL: Record<string, string> = { architecture: 'Arquitetura' };

/** Frase de cada evento que a interface mostra. Eventos internos (estado da execução) e desconhecidos ficam de fora. */
export function describeEvent(event: TimelineEvent): string | null {
  const agent =
    typeof event.data.agentId === 'string' ? (AGENT_LABEL[event.data.agentId] ?? event.data.agentId) : 'Agente';
  switch (event.type) {
    case 'workflow.started':
      return 'Execução iniciada';
    case 'context.retrieval.started':
      return 'Buscando contexto no projeto';
    case 'context.retrieved':
      return typeof event.data.filePath === 'string'
        ? `Trecho recuperado de ${event.data.filePath}`
        : 'Trecho recuperado';
    case 'agent.started':
      return `${agent} começou`;
    case 'agent.completed':
      return `${agent} concluiu`;
    case 'approval.required':
      return 'Aprovação necessária para continuar';
    case 'workflow.interrupted':
      return 'Pausado aguardando aprovação';
    case 'workflow.completed':
      return 'Workflow concluído';
    case 'workflow.failed':
      return 'Workflow falhou';
    default:
      return null;
  }
}
