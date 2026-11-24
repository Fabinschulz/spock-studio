import type { ZodType } from 'zod';

import { isTerminalState, type RunEvent, type TimelineEvent } from '../schema';
import { EngineError, type EngineClient } from './engine-client';

/** O mínimo que o acompanhamento lê de um retrato; cada tela traz o próprio esquema do resultado. */
export type FollowedSnapshot = { state: string; lastSeq: number; events: TimelineEvent[] };

export type RunSource<S extends FollowedSnapshot> = {
  onEvent(handler: (event: RunEvent) => void): Promise<() => void>;
  /** Com `sinceSeq`, o retrato traz só os eventos depois desse número. */
  snapshot(runId: string, sinceSeq?: number): Promise<S>;
};

export type FollowHandlers<S extends FollowedSnapshot> = {
  /** Assim que o pedido devolve o id. */
  started?(runId: string): void;
  /** Em ordem de `seq`, cada um uma vez: os do retrato, os que chegaram antes dele e os recuperados de uma lacuna. */
  event?(event: TimelineEvent): void;
  /** Cada retrato lido: o primeiro, os de recuperação e o do fim. */
  snapshot?(snapshot: S): void;
  /** Uma releitura falhou; o acompanhamento continua e tenta de novo no próximo evento ou ciclo. */
  lost?(): void;
  /** A escuta de eventos falhou. Com ele, o acompanhamento segue só pelas leituras; sem ele, falha. */
  deaf?(): void;
};

export type FollowOptions = {
  /** Falso: segue até a pessoa sair, porque a página de execução continua acompanhando depois de uma retomada. */
  untilDone?: boolean;
  /** Releitura periódica para o caso de o evento de término se perder. Sem ele, só os eventos acordam a leitura. */
  pollMs?: number;
  signal?: AbortSignal;
};

export type RunFollow<S extends FollowedSnapshot> = {
  /** O retrato do fim (`untilDone`); rejeita com `ABORTED` quando o acompanhamento é interrompido. */
  done: Promise<S>;
  /** Lê de novo o que faltou e devolve o retrato. */
  refresh(): Promise<S>;
  stop(): void;
};

/** O retrato pelo cliente do motor, com o esquema do resultado de cada tela. */
export function engineRunSource<S extends FollowedSnapshot>(client: EngineClient, schema: ZodType<S>): RunSource<S> {
  return {
    onEvent: (handler) => client.onEvent(handler),
    snapshot: (runId, sinceSeq) =>
      client.request('run.snapshot', sinceSeq === undefined ? { runId } : { runId, sinceSeq }, schema)
  };
}

const aborted = () => new EngineError('ABORTED', 'O acompanhamento foi interrompido.');

const isEnd = (event: TimelineEvent) =>
  event.type === 'run.completed' ||
  (event.type === 'run.state' && typeof event.data.state === 'string' && isTerminalState(event.data.state));

const timelineOf = (event: RunEvent): TimelineEvent => ({
  seq: event.seq,
  type: event.type,
  ...(event.at === undefined ? {} : { at: event.at }),
  data: event.data
});

/**
 * O único jeito de acompanhar uma execução (UI-3). Escuta antes de pedir, porque os primeiros eventos e até o fim podem
 * sair antes de o pedido voltar com o id. Entrega os eventos em ordem de `seq`; uma lacuna vira uma leitura com
 * `sinceSeq`, e o que o motor já não guarda fica para trás em vez de travar a fila.
 */
export function followRun<S extends FollowedSnapshot>(
  source: RunSource<S>,
  start: string | (() => Promise<string>),
  handlers: FollowHandlers<S> = {},
  { untilDone = true, pollMs, signal }: FollowOptions = {}
): RunFollow<S> {
  let runId: string | null = null;
  let ready = false;
  let finished = false;
  let delivered = -1;
  const early: RunEvent[] = [];
  const waiting = new Map<number, TimelineEvent>();
  let unlisten: (() => void) | null = null;
  let timer: ReturnType<typeof setTimeout> | null = null;
  let reading: Promise<S> | null = null;
  // O fim chegou por evento e o retrato com o resultado ainda não.
  let endPending = false;
  let resolveDone!: (snapshot: S) => void;
  let rejectDone!: (error: unknown) => void;
  const done = new Promise<S>((resolve, reject) => {
    resolveDone = resolve;
    rejectDone = reject;
  });
  // Quem só usa `refresh` ou `stop` não precisa tratar a interrupção.
  done.catch(() => undefined);

  const stop = () => {
    if (finished) return;
    finished = true;
    unlisten?.();
    if (timer) clearTimeout(timer);
    signal?.removeEventListener('abort', onAbort);
  };

  function onAbort() {
    stop();
    rejectDone(aborted());
  }

  const retry = () => {
    if (!reading) void read().catch(() => handlers.lost?.());
  };

  /** Entrega o que já está em sequência; `true` quando sobra evento depois de um buraco. */
  function drain(): boolean {
    for (let next = waiting.get(delivered + 1); next && !finished; next = waiting.get(delivered + 1)) {
      waiting.delete(next.seq);
      delivered = next.seq;
      handlers.event?.(next);
      // O resultado só vem no retrato.
      if (isEnd(next)) endPending = true;
    }
    for (const seq of waiting.keys()) if (seq <= delivered) waiting.delete(seq);
    return waiting.size > 0;
  }

  function accept(event: TimelineEvent) {
    if (finished || event.seq <= delivered) return;
    waiting.set(event.seq, event);
    if (ready && (drain() || endPending)) retry();
  }

  /**
   * Um número até o `lastSeq` que o retrato não trouxe o motor já não guarda: a fila pula para o próximo que existe. Um
   * buraco depois do `lastSeq` é de evento mais novo que o retrato, e a próxima leitura o traz.
   */
  function absorb(snapshot: S) {
    if (finished) return;
    handlers.snapshot?.(snapshot);
    for (const event of snapshot.events) if (event.seq > delivered) waiting.set(event.seq, event);
    if (!ready) {
      ready = true;
      for (const event of early.splice(0))
        if (event.runId === runId && event.seq > delivered) waiting.set(event.seq, timelineOf(event));
    }
    while (drain() && delivered + 1 <= snapshot.lastSeq) delivered = Math.min(...waiting.keys()) - 1;
    if (waiting.size === 0) delivered = Math.max(delivered, snapshot.lastSeq);
    if (isTerminalState(snapshot.state)) {
      endPending = false;
      if (untilDone) {
        stop();
        resolveDone(snapshot);
        return;
      }
    }
    if (waiting.size > 0 || endPending) retry();
  }

  function read(): Promise<S> {
    if (reading) return reading;
    if (runId === null) return Promise.reject(new EngineError('NOT_STARTED', 'A execução ainda não começou.'));
    reading = source.snapshot(runId, ready ? delivered : undefined).then(
      (snapshot) => {
        reading = null;
        absorb(snapshot);
        return snapshot;
      },
      (error: unknown) => {
        reading = null;
        throw error;
      }
    );
    return reading;
  }

  function poll() {
    if (finished || pollMs === undefined) return;
    timer = setTimeout(() => {
      read().then(poll, () => {
        handlers.lost?.();
        poll();
      });
    }, pollMs);
  }

  const listen = (event: RunEvent) => {
    if (finished) return;
    if (!ready) early.push(event);
    else if (event.runId === runId) accept(timelineOf(event));
  };

  if (signal?.aborted) {
    finished = true;
    rejectDone(aborted());
  } else {
    signal?.addEventListener('abort', onAbort, { once: true });
    void (async () => {
      try {
        let stopListening: (() => void) | null = null;
        try {
          stopListening = await source.onEvent(listen);
        } catch (error) {
          if (!handlers.deaf) throw error;
          handlers.deaf();
        }
        if (finished) {
          stopListening?.();
          return;
        }
        unlisten = stopListening;
        runId = typeof start === 'string' ? start : await start();
        if (finished) return;
        handlers.started?.(runId);
        await read();
        poll();
      } catch (error) {
        stop();
        rejectDone(error);
      }
    })();
  }

  return { done, refresh: read, stop };
}
