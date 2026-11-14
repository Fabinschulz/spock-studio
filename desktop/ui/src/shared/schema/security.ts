import { z } from 'zod';

import { snapshotOf } from './run';

/** Um achado nunca traz o valor encontrado: só onde está, de que tipo é e o que fazer. `fixable` diz se o plano corrige. */
export const securityFindingSchema = z.object({
  id: z.string(),
  kind: z.enum(['secret', 'unprotected-file', 'ai-permission']),
  severity: z.enum(['high', 'medium']),
  path: z.string(),
  line: z.number().nullable(),
  rule: z.string(),
  title: z.string(),
  advice: z.string(),
  fixable: z.boolean()
});

export const securityReportSchema = z.object({
  projectId: z.string(),
  scannedAt: z.string(),
  filesScanned: z.number(),
  truncated: z.boolean(),
  findings: z.array(securityFindingSchema)
});

/** A correção como ela vai ser gravada; aprovar é mandar este `planId` de volta. */
export const securityPlanSchema = z.object({
  planId: z.string(),
  projectId: z.string(),
  changes: z.array(
    z.object({
      path: z.string(),
      op: z.string(),
      diff: z.string(),
      truncated: z.boolean(),
      beforeHash: z.string().nullable(),
      afterHash: z.string()
    })
  ),
  findingIds: z.array(z.string()),
  manual: z.number()
});

export const securityFixResultSchema = z.object({
  runId: z.string(),
  kind: z.literal('security'),
  outcome: z.string(),
  changes: z.array(z.object({ path: z.string(), op: z.string(), status: z.string() })),
  fixed: z.number(),
  remaining: z.number().nullable(),
  error: z.string().nullable()
});

export const securitySnapshotSchema = snapshotOf(securityFixResultSchema);

export type SecurityFinding = z.infer<typeof securityFindingSchema>;
export type SecurityReport = z.infer<typeof securityReportSchema>;
export type SecurityPlan = z.infer<typeof securityPlanSchema>;
export type SecurityFixResult = z.infer<typeof securityFixResultSchema>;
