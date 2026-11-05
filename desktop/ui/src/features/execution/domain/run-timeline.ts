import { z } from 'zod';

import { isTerminalState, snapshotOf, type TimelineEvent } from '@/shared/schema';

/** Um trecho recuperado, com a origem que o motor conferiu: arquivo, trecho, hash do conteúdo e pontuação da busca. */
const evidenceSchema = z.object({
  filePath: z.string(),
  excerpt: z.string(),
  chunkId: z.string(),
  hash: z.string().optional(),
  score: z.number().optional()
});

/** Por que a execução falhou, como o motor registrou (`WorkflowResult.Failure`). */
const failureSchema = z.object({ code: z.string(), message: z.string() });

const impactSchema = z.object({
  schema: z.literal('engineering.impact.v1'),
  areas: z.array(z.string()),
  risks: z.array(z.string()),
  evidence: z.array(z.string())
});

/** A opinião do juiz por modelo (EV-JUDGE-001): notas por critério, justificativa e o modelo que a escreveu. Não é veredito. */
const judgmentSchema = z.object({
  criteriaVersion: z.string(),
  scores: z.record(z.string(), z.number()),
  rationale: z.string(),
  evidence: z.array(z.string()).default([]),
  model: z.string(),
  modelVersion: z.string()
});

const evaluationSchema = z.object({
  verdict: z.literal('deterministic'),
  judge: z.enum(['not_run', 'passed', 'failed']),
  tokens: z.number(),
  judgment: judgmentSchema.optional()
});

const workflowResultSchema = z.object({
  kind: z.literal('workflow'),
  // Uma execução que falhou não tem resumo: o resultado traz só `error`. Exigir o resumo fazia a página recusar o retrato inteiro.
  summary: z.string().default(''),
  error: failureSchema.optional(),
  evidence: z.array(evidenceSchema).default([]),
  limitations: z.array(z.object({ code: z.string(), message: z.string() })).default([]),
  impact: impactSchema.optional(),
  evaluation: evaluationSchema.optional(),
  /** O que aconteceu com cada ação proposta, como o motor registrou: executada, não aprovada (skipped), em conflito ou falha. */
  actions: z
    .array(
      z.object({
        id: z.string(),
        kind: z.string(),
        summary: z.string(),
        target: z.string().optional(),
        status: z.string()
      })
    )
    .optional(),
  /** Tokens gastos com o modelo, como o motor contou. Só vem em execuções que usaram modelo. */
  usage: z.object({ modelCalls: z.number(), inputTokens: z.number(), outputTokens: z.number() }).optional()
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

/** O que o motor pediu para aprovar (evento `approval.required`): só o que a política mostra, nunca o conteúdo. */
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

/** A aprovação aberta mais recente da execução, ou `null` se o motor ainda não anunciou nenhuma. */
export function pendingApproval(events: readonly TimelineEvent[]): PendingApproval | null {
  const latest = [...events].reverse().find((event) => event.type === 'approval.required');
  const parsed = latest ? approvalDataSchema.safeParse(latest.data) : null;
  if (!parsed?.success) return null;
  return {
    approvalId: parsed.data.approvalId,
    actions: parsed.data.actions.map((action) => ({ ...action, target: action.target ?? null }))
  };
}

const AGENT_LABEL: Record<string, string> = { planning: 'Planejamento', architecture: 'Arquitetura' };

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
