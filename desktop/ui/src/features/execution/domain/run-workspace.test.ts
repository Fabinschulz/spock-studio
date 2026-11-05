import { describe, expect, it } from 'vitest';

import type { TimelineEvent } from '@/shared/schema';

import { deriveEvidence, deriveStages, failureHint, riskLabel } from './run-workspace';

const event = (seq: number, type: string, data: Record<string, unknown> = {}): TimelineEvent => ({ seq, type, data });

const CATALOGUE: Record<string, TimelineEvent> = {
  retrievalStarted: event(0, 'context.retrieval.started'),
  retrieved: event(1, 'context.retrieved', { chunkId: 'a#1', filePath: 'a.md', score: 1.5, hash: 'f'.repeat(64) }),
  planningStarted: event(2, 'agent.started', { agentId: 'planning', agentVersion: '1' }),
  planningCompleted: event(3, 'agent.completed', { agentId: 'planning' }),
  architectureStarted: event(4, 'agent.started', { agentId: 'architecture', agentVersion: '2' }),
  architectureCompleted: event(5, 'agent.completed', { agentId: 'architecture' }),
  evalStarted: event(6, 'eval.started', { evalId: 'EV-JUDGE-001', kind: 'model' }),
  evalCompleted: event(7, 'eval.completed', { evalId: 'EV-JUDGE-001', kind: 'model', outcome: 'passed' }),
  approval: event(8, 'approval.required', { approvalId: 'apr-0123456789ab', actions: [] })
};
const STATES = ['running', 'awaiting_approval', 'completed', 'completed_with_limitations', 'failed', 'cancelled'];

function ids(events: TimelineEvent[], state: string) {
  return deriveStages(events, state).map((stage) => stage.id);
}

describe('etapas da execução', () => {
  it('sem eventos e sem estado final não há etapa alguma', () => {
    expect(ids([], 'running')).toEqual([]);
    expect(ids([], 'awaiting_approval')).toEqual([]);
  });

  it('nenhuma etapa aparece sem um evento que a sustente, para qualquer combinação de eventos e de estados', () => {
    const names = Object.keys(CATALOGUE);
    let checked = 0;
    for (let mask = 0; mask < 1 << names.length; mask += 1) {
      const events = names.filter((_, bit) => mask & (1 << bit)).map((name) => CATALOGUE[name]!);
      const has = (type: string, agent?: string) =>
        events.some((e) => e.type === type && (agent === undefined || e.data.agentId === agent));
      for (const state of STATES) {
        const found = ids(events, state);
        if (found.includes('context')) expect(has('context.retrieval.started') || has('context.retrieved')).toBe(true);
        if (found.includes('agent-planning'))
          expect(has('agent.started', 'planning') || has('agent.completed', 'planning')).toBe(true);
        if (found.includes('agent-architecture'))
          expect(has('agent.started', 'architecture') || has('agent.completed', 'architecture')).toBe(true);
        if (found.includes('evaluation')) expect(has('eval.started') || has('eval.completed')).toBe(true);
        if (found.includes('approval')) expect(has('approval.required')).toBe(true);
        // O resultado depende do estado, nunca de um evento inventado.
        expect(found.includes('outcome')).toBe(
          ['completed', 'completed_with_limitations', 'failed', 'cancelled'].includes(state)
        );
        // E o contrário: o que tem evento aparece.
        expect(found.includes('approval')).toBe(has('approval.required'));
        expect(found.includes('agent-planning')).toBe(
          has('agent.started', 'planning') || has('agent.completed', 'planning')
        );
        checked += 1;
      }
    }
    expect(checked).toBe((1 << names.length) * STATES.length);
  });

  it('segue a ordem do grafo e diz a versão do agente que o evento trouxe', () => {
    const events = Object.values(CATALOGUE).reverse();
    const stages = deriveStages(events, 'awaiting_approval');
    expect(stages.map((stage) => stage.id)).toEqual([
      'context',
      'agent-planning',
      'agent-architecture',
      'evaluation',
      'approval'
    ]);
    expect(stages.find((stage) => stage.id === 'agent-architecture')?.detail).toBe('Agente, versão 2');
    expect(stages.find((stage) => stage.id === 'context')?.detail).toBe('1 trecho(s) recuperado(s)');
    expect(stages.find((stage) => stage.id === 'approval')).toMatchObject({ glyph: 'running' });
  });

  it('uma etapa que começou e não terminou fica "em andamento" enquanto roda e "falhou" se a execução falhou', () => {
    const events = [CATALOGUE.planningStarted!];
    expect(deriveStages(events, 'running')[0]).toMatchObject({ id: 'agent-planning', glyph: 'running' });
    expect(deriveStages(events, 'failed')[0]).toMatchObject({ glyph: 'failed' });
    expect(deriveStages(events, 'cancelled')[0]).toMatchObject({ glyph: 'skipped' });
    expect(deriveStages([...events, CATALOGUE.planningCompleted!], 'failed')[0]).toMatchObject({ glyph: 'done' });
  });

  it('um agente que a tela não conhece aparece pelo id, sem nome inventado', () => {
    const stages = deriveStages([event(0, 'agent.started', { agentId: 'auditor' })], 'running');
    expect(stages).toEqual([{ id: 'agent-auditor', label: 'auditor', glyph: 'running', detail: 'Agente' }]);
  });
});

describe('evidências', () => {
  const result = {
    evidence: [
      { chunkId: 'a#1', filePath: 'a.md', excerpt: 'texto', score: 1.5, hash: 'abcdef0123456789'.repeat(4) },
      { chunkId: 'b#1', filePath: 'b.md', excerpt: '' }
    ],
    impact: { evidence: ['a#1'] }
  };

  it('com o resultado, mostra origem, pontuação, versão abreviada e se o impacto citou o trecho', () => {
    expect(deriveEvidence([], result)).toEqual([
      { chunkId: 'a#1', filePath: 'a.md', excerpt: 'texto', score: 1.5, hash: 'abcdef012345', citedByImpact: true },
      { chunkId: 'b#1', filePath: 'b.md', excerpt: null, score: null, hash: null, citedByImpact: false }
    ]);
  });

  it('antes do resultado, usa só os eventos de recuperação e não inventa o texto', () => {
    const live = deriveEvidence(
      [CATALOGUE.retrieved!, event(9, 'context.retrieved', { filePath: 'sem-chunk.md' })],
      null
    );
    expect(live).toEqual([
      { chunkId: 'a#1', filePath: 'a.md', excerpt: null, score: 1.5, hash: 'ffffffffffff', citedByImpact: false }
    ]);
  });

  it('sem resultado e sem evento não há evidência', () => {
    expect(deriveEvidence([], null)).toEqual([]);
  });
});

describe('textos por código', () => {
  it('só explica o que conhece e devolve o código quando não conhece', () => {
    expect(failureHint('AI_RUNTIME_UNAVAILABLE')).toMatch(/parou ou não respondeu/);
    expect(failureHint('CODIGO_NOVO')).toBeNull();
    expect(riskLabel('sem-evidencia')).toMatch(/Sem trechos/);
    expect(riskLabel('risco-novo')).toBe('risco-novo');
  });
});
