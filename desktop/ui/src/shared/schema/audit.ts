import { z } from 'zod';

/** Uma entrada tem uma das duas referências: `runId` (source `journal`) ou `auditLine` (source `audit`). */
export const projectAuditEntrySchema = z.object({
  id: z.string(),
  at: z.string(),
  category: z.string(),
  title: z.string(),
  detail: z.string(),
  source: z.string(),
  runId: z.string().nullable(),
  auditLine: z.number().nullable()
});

export const projectAuditSourceSchema = z.object({
  source: z.string(),
  label: z.string(),
  count: z.number(),
  available: z.boolean()
});

/** A contagem é da trilha inteira, sem olhar o filtro. */
export const projectAuditCategorySchema = z.object({ id: z.string(), label: z.string(), count: z.number() });

export const projectAuditTrailSchema = z.object({
  projectId: z.string(),
  generatedAt: z.string(),
  entries: z.array(projectAuditEntrySchema),
  nextBefore: z.string().nullable(),
  sources: z.array(projectAuditSourceSchema),
  categories: z.array(projectAuditCategorySchema)
});

export type ProjectAuditEntry = z.infer<typeof projectAuditEntrySchema>;
export type ProjectAuditSource = z.infer<typeof projectAuditSourceSchema>;
export type ProjectAuditCategory = z.infer<typeof projectAuditCategorySchema>;
export type ProjectAuditTrail = z.infer<typeof projectAuditTrailSchema>;
