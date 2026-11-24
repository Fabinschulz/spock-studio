import { z } from 'zod';

import { type TimelineEvent, type WorkflowSnapshot } from '@/shared/schema';

import { agentLabel } from './run-workspace';

export type { TimelineEvent };

export type RunView = {
  snapshot: WorkflowSnapshot;
  events: TimelineEvent[];
  lastSeq: number;
};

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

/** Só o que a política mostra, nunca o conteúdo da ação. */
export type PendingApproval = {
  approvalId: string;
  actions: { id: string; kind: string; summary: string; target: string | null }[];
};

const pendingActionSchema = z.object({
  id: z.string(),
  kind: z.string(),
  summary: z.string(),
  target: z.string().optional()
});

const approvalDataSchema = z.object({ approvalId: z.string(), actions: z.array(pendingActionSchema).default([]) });

export function pendingApproval(events: readonly TimelineEvent[]): PendingApproval | null {
  const latest = [...events].reverse().find((event) => event.type === 'approval.required');
  const parsed = latest ? approvalDataSchema.safeParse(latest.data) : null;
  if (!parsed?.success) return null;
  return {
    approvalId: parsed.data.approvalId,
    actions: parsed.data.actions.map((action) => ({ ...action, target: action.target ?? null }))
  };
}

/** Eventos internos (estado da execução) e desconhecidos ficam de fora. */
export function describeEvent(event: TimelineEvent): string | null {
  const agent = typeof event.data.agentId === 'string' ? agentLabel(event.data.agentId) : 'Agente';
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
    case 'eval.started':
      return event.data.kind === 'model' ? 'Avaliando o resultado com o juiz por modelo' : null;
    case 'eval.completed':
      if (event.data.kind !== 'model') return null;
      if (event.data.outcome === 'passed') return 'Avaliação por modelo registrada: aprovada nos critérios';
      if (event.data.outcome === 'failed') return 'Avaliação por modelo registrada: abaixo do mínimo em algum critério';
      return 'O juiz por modelo não rodou';
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
