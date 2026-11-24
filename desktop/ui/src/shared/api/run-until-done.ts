import { z } from 'zod';

import { snapshotOf } from '../schema';
import type { EngineClient } from './engine-client';
import { engineRunSource, followRun } from './follow-run';

const anySnapshotSchema = snapshotOf(z.unknown());

/** Inicia uma execução e espera ela terminar; quem chama lê o resultado com o esquema dele. */
export async function runUntilDone(client: EngineClient, start: () => Promise<string>): Promise<string> {
  let runId = '';
  await followRun(engineRunSource(client, anySnapshotSchema), start, {
    started: (id) => {
      runId = id;
    }
  }).done;
  return runId;
}
