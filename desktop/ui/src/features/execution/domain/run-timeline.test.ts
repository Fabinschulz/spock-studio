import { describe, expect, it } from 'vitest';

import { applyLive, describeEvent, fromSnapshot, type WorkflowSnapshot } from './run-timeline';

const base: WorkflowSnapshot = {
  runId: 'run-1',
  projectId: 'p-aaaaaaaaaaaa',
  kind: 'workflow',
  state: 'running',
  lastSeq: 1,
  events: [
    { seq: 0, type: 'run.state', data: { state: 'running' } },
    { seq: 1, type: 'workflow.started', data: {} }
  ],
  result: null
};

describe('linha do tempo da execução', () => {
  it('aceita só o próximo número e ignora repetido ou atrasado', () => {
    const view = fromSnapshot(base);
    const next = applyLive(view, { seq: 2, type: 'agent.started', data: { agentId: 'architecture' } });
    expect(next.gap).toBe(false);
    expect(next.view.lastSeq).toBe(2);
    expect(applyLive(next.view, { seq: 2, type: 'agent.started', data: {} }).view).toBe(next.view);
    expect(applyLive(next.view, { seq: 1, type: 'workflow.started', data: {} }).view).toBe(next.view);
  });

  it('acusa lacuna em vez de mostrar sequência com buraco', () => {
    const outcome = applyLive(fromSnapshot(base), { seq: 4, type: 'agent.completed', data: {} });
    expect(outcome.gap).toBe(true);
    expect(outcome.view.events.map((event) => event.seq)).toEqual([0, 1]);
  });

  it('um retrato novo completa o que faltou sem duplicar', () => {
    const first = fromSnapshot(base);
    const merged = fromSnapshot(
      {
        ...base,
        lastSeq: 3,
        events: [
          { seq: 2, type: 'x', data: {} },
          { seq: 3, type: 'y', data: {} }
        ]
      },
      first
    );
    expect(merged.events.map((event) => event.seq)).toEqual([0, 1, 2, 3]);
    expect(merged.lastSeq).toBe(3);
  });

  it('o estado acompanha run.state e run.completed', () => {
    const done = applyLive(fromSnapshot(base), { seq: 2, type: 'run.completed', data: { state: 'failed' } });
    expect(done.view.snapshot.state).toBe('failed');
  });

  it('descreve só o que aconteceu e esconde eventos internos', () => {
    expect(describeEvent({ seq: 1, type: 'agent.completed', data: { agentId: 'architecture' } })).toBe(
      'Arquitetura concluiu'
    );
    expect(describeEvent({ seq: 1, type: 'context.retrieved', data: { filePath: 'docs/a.md' } })).toBe(
      'Trecho recuperado de docs/a.md'
    );
    expect(describeEvent({ seq: 1, type: 'run.state', data: { state: 'running' } })).toBeNull();
    expect(describeEvent({ seq: 1, type: 'desconhecido', data: {} })).toBeNull();
  });
});
