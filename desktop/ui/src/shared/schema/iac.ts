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
