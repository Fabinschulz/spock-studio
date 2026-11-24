import { describe, expect, it, vi } from 'vitest';

import type { RunEvent, TimelineEvent } from '../schema';
import { followRun, type FollowedSnapshot } from './follow-run';

type Snapshot = FollowedSnapshot & { result: string | null };

/**
 * Um motor de mentira: guarda os eventos de cada execução como o RunHandle (seq a partir de 0, só os últimos `keep`),
 * publica para quem escuta e responde ao retrato com `sinceSeq`.
 */
class FakeEngine {
  private listeners = new Set<(event: RunEvent) => void>();
  private events: TimelineEvent[] = [];
  state = 'running';
  result: string | null = null;
  keep = Number.POSITIVE_INFINITY;
  reads: (number | undefined)[] = [];

  /** O evento entra no histórico; `deliver` falso simula a perda no caminho até a tela. */
  publish(type: string, data: Record<string, unknown> = {}, { deliver = true, runId = 'run-1' } = {}) {
    const event = { seq: this.events.length, type, data };
    if (runId === 'run-1') this.events.push(event);
    if (deliver) this.listeners.forEach((listen) => listen({ v: 1, kind: 'event', runId, ...event }));
    return event;
  }

  finish(result = 'ok') {
    this.state = 'completed';
    this.result = result;
    this.publish('run.completed', { state: 'completed' });
  }

  source = {
    onEvent: vi.fn(async (handler: (event: RunEvent) => void) => {
      this.listeners.add(handler);
      return () => this.listeners.delete(handler);
    }),
    snapshot: vi.fn(async (_runId: string, sinceSeq?: number): Promise<Snapshot> => {
      this.reads.push(sinceSeq);
      const kept = this.events.slice(-this.keep);
      return {
        state: this.state,
        lastSeq: this.events.length - 1,
        events: kept.filter((event) => sinceSeq === undefined || event.seq > sinceSeq),
        result: this.result
      };
    })
  };

  get listening() {
    return this.listeners.size;
  }
}

const collect = () => {
  const seen: number[] = [];
  return { seen, handlers: { event: (event: TimelineEvent) => seen.push(event.seq) } };
};

describe('followRun', () => {
  it('evento que sai antes de o pedido voltar com o id aparece no progresso, uma vez e em ordem', async () => {
    const engine = new FakeEngine();
    const { seen, handlers } = collect();

    const follow = followRun(
      engine.source,
      async () => {
        engine.publish('run.plan');
        engine.publish('step.started');
        return 'run-1';
      },
      handlers
    );
    await vi.waitFor(() => expect(engine.reads).toHaveLength(1));
    engine.publish('step.completed');
    engine.finish();

    await expect(follow.done).resolves.toMatchObject({ state: 'completed', result: 'ok' });
    expect(seen).toEqual([0, 1, 2, 3]);
    expect(engine.listening).toBe(0);
  });

  it('lacuna na numeração vira uma leitura desde o último entregue, sem perder nem repetir', async () => {
    const engine = new FakeEngine();
    const { seen, handlers } = collect();
    const follow = followRun(engine.source, 'run-1', handlers);
    await vi.waitFor(() => expect(engine.reads).toEqual([undefined]));

    engine.publish('agent.output', { line: 'a' });
    engine.publish('agent.output', { line: 'b' }, { deliver: false });
    engine.publish('agent.output', { line: 'c' }, { deliver: false });
    engine.publish('agent.output', { line: 'd' });
    await vi.waitFor(() => expect(seen).toEqual([0, 1, 2, 3]));
    engine.finish();

    await follow.done;
    expect(engine.reads.slice(0, 2)).toEqual([undefined, 0]);
    expect(seen).toEqual([0, 1, 2, 3, 4]);
  });

  it('o que o motor já não guarda fica para trás em vez de travar a fila', async () => {
    const engine = new FakeEngine();
    engine.keep = 3;
    for (let i = 0; i < 10; i += 1) engine.publish('agent.output', { line: String(i) }, { deliver: false });
    const { seen, handlers } = collect();

    const follow = followRun(engine.source, 'run-1', handlers, { untilDone: false });
    await vi.waitFor(() => expect(seen).toEqual([7, 8, 9]));
    engine.publish('agent.output', { line: '10' });

    await vi.waitFor(() => expect(seen).toEqual([7, 8, 9, 10]));
    follow.stop();
    expect(engine.listening).toBe(0);
  });

  it('execução que terminou antes da primeira leitura sai com o retrato, sem esperar evento', async () => {
    const engine = new FakeEngine();
    const follow = followRun(engine.source, async () => {
      engine.finish('pronto');
      return 'run-1';
    });

    await expect(follow.done).resolves.toMatchObject({ result: 'pronto' });
    expect(engine.source.snapshot).toHaveBeenCalledTimes(1);
  });

  it('eventos de outra execução não entram', async () => {
    const engine = new FakeEngine();
    const { seen, handlers } = collect();
    const follow = followRun(engine.source, 'run-1', handlers);
    await vi.waitFor(() => expect(engine.reads).toHaveLength(1));

    engine.publish('agent.output', {}, { runId: 'run-9' });
    engine.finish();

    await follow.done;
    expect(seen).toEqual([0]);
  });

  it('com o término perdido no caminho, a releitura periódica encontra o fim', async () => {
    const engine = new FakeEngine();
    const follow = followRun(engine.source, 'run-1', {}, { pollMs: 5 });
    await vi.waitFor(() => expect(engine.reads.length).toBeGreaterThan(1));

    engine.state = 'completed';
    engine.result = 'ok';
    engine.publish('run.completed', { state: 'completed' }, { deliver: false });

    await expect(follow.done).resolves.toMatchObject({ result: 'ok' });
  });

  it('interromper encerra na hora com ABORTED e para de escutar', async () => {
    const engine = new FakeEngine();
    const controller = new AbortController();
    const follow = followRun(engine.source, 'run-1', {}, { signal: controller.signal });
    await vi.waitFor(() => expect(engine.reads).toHaveLength(1));

    controller.abort();

    await expect(follow.done).rejects.toMatchObject({ code: 'ABORTED' });
    expect(engine.listening).toBe(0);
  });

  it('sem untilDone, segue depois do fim: uma retomada com novos eventos continua chegando', async () => {
    const engine = new FakeEngine();
    const snapshots: string[] = [];
    const { seen, handlers } = collect();
    const follow = followRun(
      engine.source,
      'run-1',
      { ...handlers, snapshot: (snapshot) => snapshots.push(snapshot.state) },
      { untilDone: false }
    );
    await vi.waitFor(() => expect(engine.reads).toHaveLength(1));

    engine.state = 'requires_intervention';
    engine.publish('run.state', { state: 'requires_intervention' });
    await vi.waitFor(() => expect(snapshots).toContain('requires_intervention'));
    engine.state = 'running';
    engine.publish('run.state', { state: 'running' });
    engine.finish();

    await vi.waitFor(() => expect(snapshots.at(-1)).toBe('completed'));
    expect(seen).toEqual([0, 1, 2]);
    follow.stop();
  });

  it('erro ao iniciar rejeita e solta a escuta', async () => {
    const engine = new FakeEngine();
    const follow = followRun(engine.source, async () => {
      throw new Error('recusado');
    });

    await expect(follow.done).rejects.toThrow('recusado');
    expect(engine.listening).toBe(0);
  });
});
