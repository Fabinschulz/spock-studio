import { z } from 'zod';

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
  pinnedAt: z.string()
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
  minimumVersion: z.string(),
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
  version: z.string(),
  platform: terraformPlatformSchema,
  archiveUrl: z.string(),
  shasumsUrl: z.string(),
  signatureUrl: z.string(),
  signingKey: z.string(),
  publishedAt: z.string(),
  changelogUrl: z.string().nullable(),
  currentVersion: z.string().nullable(),
  isUpdate: z.boolean(),
  impacts: z.array(z.string())
});

export const terraformOfferResultSchema = z.object({ offer: terraformOfferSchema });

export type TerraformSource = z.infer<typeof terraformSourceSchema>;
export type TerraformState = z.infer<typeof terraformStateSchema>;
export type TerraformInstallation = z.infer<typeof terraformInstallationSchema>;
export type TerraformStatus = z.infer<typeof terraformStatusSchema>;
export type TerraformOffer = z.infer<typeof terraformOfferSchema>;
export type ProjectTerraform = z.infer<typeof projectTerraformSchema>;
