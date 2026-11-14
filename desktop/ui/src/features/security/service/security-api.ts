import { runUntilDone, type EngineClient } from '@/shared/api';
import {
  runStartedSchema,
  securityPlanSchema,
  securityReportSchema,
  securitySnapshotSchema,
  type SecurityPlan,
  type SecurityReport
} from '@/shared/schema';
import type { z } from 'zod';

export type SecuritySnapshot = z.infer<typeof securitySnapshotSchema>;

export interface SecurityApi {
  scan(projectId: string): Promise<SecurityReport>;
  plan(projectId: string): Promise<SecurityPlan>;
  fix(projectId: string, planId: string): Promise<SecuritySnapshot>;
  revert(projectId: string, runId: string): Promise<void>;
}

export function createSecurityApi(client: EngineClient): SecurityApi {
  return {
    scan: (projectId) => client.request('security.scan', { projectId }, securityReportSchema),
    plan: (projectId) => client.request('security.plan', { projectId }, securityPlanSchema),
    fix: async (projectId, planId) => {
      const runId = await runUntilDone(
        client,
        async () => (await client.request('security.fix', { projectId, planId }, runStartedSchema)).runId
      );
      return client.request('run.snapshot', { runId }, securitySnapshotSchema);
    },
    revert: async (projectId, runId) => {
      await runUntilDone(
        client,
        async () => (await client.request('harness.revert', { projectId, runId }, runStartedSchema)).runId
      );
    }
  };
}
