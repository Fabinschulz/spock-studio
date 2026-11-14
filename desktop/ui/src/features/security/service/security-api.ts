import type { EngineClient } from '@/shared/api';
import {
  isTerminalState,
  runStartedSchema,
  securityPlanSchema,
  securityReportSchema,
  securitySnapshotSchema,
  snapshotOf,
  type RunEvent,
  type SecurityPlan,
  type SecurityReport
} from '@/shared/schema';
import { z } from 'zod';

export type SecuritySnapshot = z.infer<typeof securitySnapshotSchema>;

const anySnapshotSchema = snapshotOf(z.unknown());

export interface SecurityApi {
  scan(projectId: string): Promise<SecurityReport>;
  plan(projectId: string): Promise<SecurityPlan>;
  fix(projectId: string, planId: string): Promise<SecuritySnapshot>;
  revert(projectId: string, runId: string): Promise<void>;
}

export function createSecurityApi(client: EngineClient): SecurityApi {
  /** Escuta antes de pedir: o fim pode chegar antes de o pedido voltar com o id. */
  async function untilDone(start: () => Promise<string>): Promise<string> {
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

  return {
    scan: (projectId) => client.request('security.scan', { projectId }, securityReportSchema),
    plan: (projectId) => client.request('security.plan', { projectId }, securityPlanSchema),
    fix: async (projectId, planId) => {
      const runId = await untilDone(
        async () => (await client.request('security.fix', { projectId, planId }, runStartedSchema)).runId
      );
      return client.request('run.snapshot', { runId }, securitySnapshotSchema);
    },
    revert: async (projectId, runId) => {
      await untilDone(
        async () => (await client.request('harness.revert', { projectId, runId }, runStartedSchema)).runId
      );
    }
  };
}
