import { z } from 'zod';

import { isTerminalState, snapshotOf, type RunEvent } from '../schema';
import type { EngineClient } from './engine-client';

const anySnapshotSchema = snapshotOf(z.unknown());

/**
 * Inicia uma execução e espera ela terminar. Escuta antes de pedir: o fim pode chegar antes de o pedido voltar com o id, e
 * o retrato confere o estado caso o evento já tenha passado.
 */
export async function runUntilDone(client: EngineClient, start: () => Promise<string>): Promise<string> {
  const finished = new Set<string>();
  let wake: (() => void) | null = null;
  const stop = await client.onEvent((event: RunEvent) => {
    if (event.type !== 'run.completed') return;
    finished.add(event.runId);
    wake?.();
  });
  try {
    const runId = await start();
    while (!finished.has(runId)) {
      if (isTerminalState((await client.request('run.snapshot', { runId }, anySnapshotSchema)).state)) break;
      await new Promise<void>((resolve) => {
        wake = resolve;
        if (finished.has(runId)) resolve();
      });
    }
    return runId;
  } finally {
    stop();
  }
}
