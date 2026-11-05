import type { GlyphState } from '@/shared/components/ui';
import { isTerminalState, type TimelineEvent } from '@/shared/schema';

/**
 * O que a tela de execução mostra além da linha do tempo, derivado só do que o motor publicou: eventos e resultado.
 * Nada aqui supõe etapa, agente ou avaliação que não tenha um evento. A ordem das etapas é a do grafo
 * (contexto, planejamento, arquitetura, avaliação, aprovação, resultado), mas uma etapa só aparece se houve evento dela.
 */

export type StageView = {
  id: string;
  label: string;
  glyph: GlyphState;
  /** Frase curta sobre o que aconteceu nesta etapa; `null` quando os eventos não dizem mais que o próprio estado. */
  detail: string | null;
};

const AGENT_LABEL: Record<string, string> = { planning: 'Planejamento', architecture: 'Arquitetura' };
const AGENT_ORDER = ['planning', 'architecture'] as const;

/** Nome mostrado de um agente; um agente que o motor passou a publicar e a tela ainda não conhece aparece pelo id. */
export function agentLabel(agentId: string): string {
  return AGENT_LABEL[agentId] ?? agentId;
}

function text(value: unknown): string | null {
  return typeof value === 'string' && value.length > 0 ? value : null;
}

function ofType(events: readonly TimelineEvent[], type: string): TimelineEvent[] {
  return events.filter((event) => event.type === type);
}

/** Estado de uma etapa que começou e ainda não terminou: se a execução já acabou, ela parou aí. */
function unfinished(state: string): GlyphState {
  if (state === 'failed') return 'failed';
  return isTerminalState(state) ? 'skipped' : 'running';
}

export function deriveStages(events: readonly TimelineEvent[], state: string): StageView[] {
  const stages: StageView[] = [];

  const retrievals = ofType(events, 'context.retrieval.started');
  const retrieved = ofType(events, 'context.retrieved');
  if (retrievals.length > 0 || retrieved.length > 0) {
    const agentStarted = events.some((event) => event.type === 'agent.started');
    stages.push({
      id: 'context',
      label: 'Contexto do projeto',
      glyph: agentStarted || isTerminalState(state) ? 'done' : 'running',
      detail: retrieved.length > 0 ? `${retrieved.length} trecho(s) recuperado(s)` : 'Busca iniciada'
    });
  }

  const seen = new Set(
    events.filter((event) => event.type.startsWith('agent.')).flatMap((event) => text(event.data.agentId) ?? [])
  );
  const agents = [...AGENT_ORDER.filter((id) => seen.has(id)), ...[...seen].filter((id) => !AGENT_LABEL[id])];
  for (const id of agents) {
    const started = ofType(events, 'agent.started').find((event) => event.data.agentId === id);
    const completed = ofType(events, 'agent.completed').some((event) => event.data.agentId === id);
    const version = text(started?.data.agentVersion);
    stages.push({
      id: `agent-${id}`,
      label: agentLabel(id),
      glyph: completed ? 'done' : unfinished(state),
      detail: version ? `Agente, versão ${version}` : 'Agente'
    });
  }

  const evals = events.filter((event) => event.type === 'eval.started' || event.type === 'eval.completed');
  if (evals.length > 0) {
    const completed = ofType(events, 'eval.completed').at(-1);
    const outcome = text(completed?.data.outcome);
    stages.push({
      id: 'evaluation',
      label: 'Avaliação',
      glyph: !completed
        ? unfinished(state)
        : outcome === 'passed'
          ? 'done'
          : outcome === 'failed'
            ? 'warning'
            : 'skipped',
      detail: !completed ? null : (EVAL_OUTCOME[outcome ?? ''] ?? null)
    });
  }

  if (events.some((event) => event.type === 'approval.required')) {
    stages.push({
      id: 'approval',
      label: 'Aprovação',
      glyph: state === 'awaiting_approval' ? 'running' : 'done',
      detail: state === 'awaiting_approval' ? 'Esperando a sua decisão' : 'Decisão registrada'
    });
  }

  if (isTerminalState(state)) {
    stages.push({
      id: 'outcome',
      label: 'Resultado',
      glyph: OUTCOME_GLYPH[state] ?? 'skipped',
      detail: null
    });
  }
  return stages;
}

const EVAL_OUTCOME: Record<string, string> = {
  passed: 'Aprovada nos critérios',
  failed: 'Abaixo do mínimo em algum critério',
  not_run: 'Não rodou',
  blocked: 'Bloqueada'
};

const OUTCOME_GLYPH: Record<string, GlyphState> = {
  completed: 'done',
  completed_with_limitations: 'warning',
  failed: 'failed',
  requires_intervention: 'warning',
  cancelled: 'skipped'
};

/** Trecho recuperado como a tela o mostra: o que o motor publicou, sem inventar resumo nem origem. */
export type EvidenceView = {
  chunkId: string;
  filePath: string;
  excerpt: string | null;
  score: number | null;
  /** Primeiros caracteres do hash do conteúdo, para reconhecer a versão do arquivo. */
  hash: string | null;
  /** O impacto técnico cita este trecho. */
  citedByImpact: boolean;
};

type ResultEvidence = {
  chunkId: string;
  filePath: string;
  excerpt: string;
  score?: number | undefined;
  hash?: string | undefined;
};

/**
 * Antes do resultado, os trechos vêm dos eventos `context.retrieved` (sem o texto, que só o resultado traz). Com o resultado,
 * vale a lista dele: é a que o motor conferiu e passou pela redação de segredos.
 */
export function deriveEvidence(
  events: readonly TimelineEvent[],
  result: { evidence: readonly ResultEvidence[]; impact?: { evidence: readonly string[] } | undefined } | null
): EvidenceView[] {
  const cited = new Set(result?.impact?.evidence ?? []);
  if (result) {
    return result.evidence.map((item) => ({
      chunkId: item.chunkId,
      filePath: item.filePath,
      excerpt: item.excerpt.length > 0 ? item.excerpt : null,
      score: item.score ?? null,
      hash: item.hash ? item.hash.slice(0, 12) : null,
      citedByImpact: cited.has(item.chunkId)
    }));
  }
  return ofType(events, 'context.retrieved').flatMap((event) => {
    const chunkId = text(event.data.chunkId);
    const filePath = text(event.data.filePath);
    if (!chunkId || !filePath) return [];
    const hash = text(event.data.hash);
    return [
      {
        chunkId,
        filePath,
        excerpt: null,
        score: typeof event.data.score === 'number' ? event.data.score : null,
        hash: hash ? hash.slice(0, 12) : null,
        citedByImpact: false
      }
    ];
  });
}

/** Motivo e o que fazer, por código de falha do motor. O texto do motor continua sendo mostrado junto. */
const FAILURE_HINT: Record<string, string> = {
  AI_RUNTIME_UNAVAILABLE: 'O runtime de IA parou ou não respondeu. Você pode iniciar o pedido de novo.',
  AI_RUNTIME_TIMEOUT: 'O runtime de IA passou do tempo limite. Você pode iniciar o pedido de novo.',
  AI_RUNTIME_INVALID_RESPONSE: 'O runtime de IA respondeu fora do contrato esperado.',
  WORKFLOW_FAILED: 'O motor encontrou uma falha interna ao conduzir a execução.'
};

export function failureHint(code: string): string | null {
  return FAILURE_HINT[code] ?? null;
}

/** Riscos do impacto técnico, por código. */
const RISK_LABEL: Record<string, string> = {
  'sem-evidencia': 'Sem trechos do projeto que sustentem a análise.',
  'sem-stack': 'Nenhuma stack foi identificada no projeto.'
};

export function riskLabel(code: string): string {
  return RISK_LABEL[code] ?? code;
}
