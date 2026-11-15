import { z } from 'zod';

/** OpenTofu é o padrão: gratuito e de código aberto. O Terraform da HashiCorp segue disponível. */
export const iacEngineSchema = z.enum(['openTofu', 'terraform']);

export const iacRuntimeSourceSchema = z.enum(['managed', 'external']);

export const iacRuntimeStateSchema = z.enum([
  'ready',
  'externalAvailable',
  'missing',
  'declined',
  'incompatible',
  'unsupportedPlatform'
]);

export const iacPlatformSchema = z.object({ os: z.string(), arch: z.string() });

/** O caminho chega abreviado (a pasta pessoal vira "~"), como os caminhos de projeto. */
export const iacInstallationSchema = z.object({
  engine: iacEngineSchema,
  source: iacRuntimeSourceSchema,
  version: z.string(),
  path: z.string(),
  compatible: z.boolean(),
  verified: z.boolean(),
  adopted: z.boolean(),
  problem: z.string().nullable(),
  installedAt: z.string().nullable()
});

export const iacRuntimePinSchema = z.object({
  version: z.string(),
  source: iacRuntimeSourceSchema,
  pinnedAt: z.string(),
  engine: iacEngineSchema
});

export const iacEngineInfoSchema = z.object({
  engine: iacEngineSchema,
  name: z.string(),
  license: z.string(),
  minimumVersion: z.string(),
  preferred: z.boolean(),
  available: z.boolean()
});

export const iacRequirementSchema = z.object({ file: z.string(), constraint: z.string(), valid: z.boolean() });

export const projectRuntimeSchema = z.object({
  projectId: z.string(),
  requirements: z.array(iacRequirementSchema),
  pin: iacRuntimePinSchema.nullable(),
  selected: iacInstallationSchema.nullable(),
  automatic: z.boolean(),
  problem: z.string().nullable()
});

export const iacRuntimeStatusSchema = z.object({
  state: iacRuntimeStateSchema,
  platform: iacPlatformSchema.nullable(),
  engines: z.array(iacEngineInfoSchema),
  installations: z.array(iacInstallationSchema),
  selected: iacInstallationSchema.nullable(),
  project: projectRuntimeSchema.nullable(),
  declinedAt: z.string().nullable(),
  message: z.string()
});

export const iacRuntimeStatusResultSchema = z.object({ runtime: iacRuntimeStatusSchema });

/** A proposta que a pessoa autoriza: o offerId amarra versão, plataforma, endereços oficiais e chave de assinatura. */
export const iacRuntimeOfferSchema = z.object({
  offerId: z.string(),
  engine: iacEngineSchema,
  engineName: z.string(),
  license: z.string(),
  version: z.string(),
  platform: iacPlatformSchema,
  archiveUrl: z.string(),
  shasumsUrl: z.string(),
  signatureUrl: z.string(),
  signingKey: z.string(),
  publishedAt: z.string().nullable(),
  changelogUrl: z.string().nullable(),
  currentVersion: z.string().nullable(),
  isUpdate: z.boolean(),
  impacts: z.array(z.string())
});

export const iacRuntimeOfferResultSchema = z.object({ offer: iacRuntimeOfferSchema });

export type IacEngine = z.infer<typeof iacEngineSchema>;
export type IacEngineInfo = z.infer<typeof iacEngineInfoSchema>;
export type IacRuntimeSource = z.infer<typeof iacRuntimeSourceSchema>;
export type IacRuntimeState = z.infer<typeof iacRuntimeStateSchema>;
export type IacInstallation = z.infer<typeof iacInstallationSchema>;
export type IacRuntimeStatus = z.infer<typeof iacRuntimeStatusSchema>;
export type IacRuntimeOffer = z.infer<typeof iacRuntimeOfferSchema>;
export type ProjectRuntime = z.infer<typeof projectRuntimeSchema>;

export const iacDiagnosticSchema = z.object({
  severity: z.string(),
  summary: z.string(),
  detail: z.string().nullable(),
  file: z.string().nullable(),
  line: z.number().nullable()
});

export const iacFolderValidationSchema = z.object({
  folder: z.string(),
  valid: z.boolean(),
  formatted: z.boolean(),
  diagnostics: z.array(iacDiagnosticSchema),
  error: z.string().nullable()
});

export const iacResourceChangeSchema = z.object({ address: z.string(), type: z.string(), action: z.string() });

/** Só endereços, tipos e ações: valores de atributo nunca saem do arquivo de plano. */
export const iacPlanSummarySchema = z.object({
  create: z.number(),
  update: z.number(),
  replace: z.number(),
  delete: z.number(),
  read: z.number(),
  unchanged: z.number(),
  changes: z.array(iacResourceChangeSchema),
  outputsChanged: z.array(z.string())
});

export const iacFindingSchema = z.object({
  severity: z.enum(['warning', 'danger']),
  rule: z.string(),
  address: z.string(),
  message: z.string()
});

export const iacApprovalSchema = z.object({
  planId: z.string(),
  configHash: z.string(),
  approvedAt: z.string(),
  destroyConfirmed: z.boolean(),
  risksAccepted: z.boolean(),
  workspaceConfirmed: z.string().nullable()
});

/** O resultado de um apply e a prova: verified só quando o plano seguinte não tem mudanças. */
export const iacApplyRunSchema = z.object({
  applied: z.boolean(),
  verified: z.boolean(),
  remaining: iacPlanSummarySchema.nullable(),
  managedResources: z.number().nullable(),
  outputs: z.record(z.string(), z.string()),
  error: z.string().nullable()
});

export const iacOperationSchema = z.object({
  id: z.string(),
  projectId: z.string(),
  kind: z.enum(['validate', 'plan', 'apply']),
  status: z.enum(['succeeded', 'failed']),
  engine: iacEngineSchema,
  version: z.string(),
  root: z.string(),
  folder: z.string().nullable(),
  workspace: z.string().nullable(),
  connectionId: z.string().nullable(),
  configHash: z.string(),
  startedAt: z.string(),
  finishedAt: z.string(),
  requestedBy: z.string(),
  validation: z.array(iacFolderValidationSchema).nullable(),
  planId: z.string().nullable(),
  hasChanges: z.boolean().nullable(),
  summary: iacPlanSummarySchema.nullable(),
  findings: z.array(iacFindingSchema).nullable(),
  error: z.string().nullable(),
  backend: z.string().nullable(),
  applyBlocker: z.string().nullable(),
  appliedBy: z.string().nullable(),
  planOperationId: z.string().nullable(),
  approval: iacApprovalSchema.nullable(),
  apply: iacApplyRunSchema.nullable()
});

export const iacOperationResultSchema = z.object({ operation: iacOperationSchema });
export const iacOperationListSchema = z.object({ operations: z.array(iacOperationSchema) });
export const iacLayoutSchema = z.object({
  layout: z.object({ projectId: z.string(), root: z.string().nullable(), folders: z.array(z.string()) })
});

export type IacOperation = z.infer<typeof iacOperationSchema>;
export type IacLayout = z.infer<typeof iacLayoutSchema>['layout'];
