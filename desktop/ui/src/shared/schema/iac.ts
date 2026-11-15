import { z } from 'zod';

/** OpenTofu é o padrão: gratuito e de código aberto. O Terraform da HashiCorp segue disponível. */
export const iacEngineSchema = z.enum(['openTofu', 'terraform']);

export const terraformSourceSchema = z.enum(['managed', 'external']);

export const terraformStateSchema = z.enum([
  'ready',
  'externalAvailable',
  'missing',
  'declined',
  'incompatible',
  'unsupportedPlatform'
]);

export const terraformPlatformSchema = z.object({ os: z.string(), arch: z.string() });

/** O caminho chega abreviado (a pasta pessoal vira "~"), como os caminhos de projeto. */
export const terraformInstallationSchema = z.object({
  engine: iacEngineSchema,
  source: terraformSourceSchema,
  version: z.string(),
  path: z.string(),
  compatible: z.boolean(),
  verified: z.boolean(),
  adopted: z.boolean(),
  problem: z.string().nullable(),
  installedAt: z.string().nullable()
});

export const terraformPinSchema = z.object({
  version: z.string(),
  source: terraformSourceSchema,
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

export const terraformRequirementSchema = z.object({ file: z.string(), constraint: z.string(), valid: z.boolean() });

export const projectTerraformSchema = z.object({
  projectId: z.string(),
  requirements: z.array(terraformRequirementSchema),
  pin: terraformPinSchema.nullable(),
  selected: terraformInstallationSchema.nullable(),
  automatic: z.boolean(),
  problem: z.string().nullable()
});

export const terraformStatusSchema = z.object({
  state: terraformStateSchema,
  platform: terraformPlatformSchema.nullable(),
  engines: z.array(iacEngineInfoSchema),
  installations: z.array(terraformInstallationSchema),
  selected: terraformInstallationSchema.nullable(),
  project: projectTerraformSchema.nullable(),
  declinedAt: z.string().nullable(),
  message: z.string()
});

export const terraformStatusResultSchema = z.object({ terraform: terraformStatusSchema });

/** A proposta que a pessoa autoriza: o offerId amarra versão, plataforma, endereços oficiais e chave de assinatura. */
export const terraformOfferSchema = z.object({
  offerId: z.string(),
  engine: iacEngineSchema,
  engineName: z.string(),
  license: z.string(),
  version: z.string(),
  platform: terraformPlatformSchema,
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

export const terraformOfferResultSchema = z.object({ offer: terraformOfferSchema });

export type IacEngine = z.infer<typeof iacEngineSchema>;
export type IacEngineInfo = z.infer<typeof iacEngineInfoSchema>;
export type TerraformSource = z.infer<typeof terraformSourceSchema>;
export type TerraformState = z.infer<typeof terraformStateSchema>;
export type TerraformInstallation = z.infer<typeof terraformInstallationSchema>;
export type TerraformStatus = z.infer<typeof terraformStatusSchema>;
export type TerraformOffer = z.infer<typeof terraformOfferSchema>;
export type ProjectTerraform = z.infer<typeof projectTerraformSchema>;
