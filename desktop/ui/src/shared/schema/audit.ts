import { z } from 'zod';

/**
 * Uma entrada da trilha de auditoria (`audit.read`). `source` diz onde ela está guardada: `journal` (o diário de uma execução,
 * com `runId`) ou `audit` (o arquivo de auditoria, com `auditLine`). Uma entrada tem uma das duas referências.
 */
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

/** Uma categoria e quantas entradas ela tem na trilha inteira, sem olhar o filtro. */
export const projectAuditCategorySchema = z.object({ id: z.string(), label: z.string(), count: z.number() });

/** A trilha de auditoria do projeto, da entrada mais recente à mais antiga. `nextBefore` é o corte da página seguinte. */
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
