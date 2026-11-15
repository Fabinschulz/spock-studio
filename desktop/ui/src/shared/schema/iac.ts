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
  apply: iacApplyRunSchema.nullable(),
  stateMode: z.enum(['remote', 'localAbsolute', 'localInProject']).nullable(),
  localStatePath: z.string().nullable(),
  localStateHash: z.string().nullable(),
  lockId: z.string().nullable()
});

export const iacOperationResultSchema = z.object({ operation: iacOperationSchema });
export const iacOperationListSchema = z.object({ operations: z.array(iacOperationSchema) });
export const iacLayoutSchema = z.object({
  layout: z.object({ projectId: z.string(), root: z.string().nullable(), folders: z.array(z.string()) })
});

export type IacOperation = z.infer<typeof iacOperationSchema>;
export type IacLayout = z.infer<typeof iacLayoutSchema>['layout'];

export const iacStateModeSchema = z.enum(['remote', 'localAbsolute', 'localInProject']);

export const iacStateRiskSchema = z.object({
  level: z.enum(['info', 'warning', 'danger']),
  rule: z.string(),
  message: z.string()
});

/** Onde vive o state de uma raiz e o que está arriscado. Só configurações seguras do backend; nenhuma credencial. */
export const iacStateRootSchema = z.object({
  folder: z.string(),
  backend: z.string(),
  mode: iacStateModeSchema,
  settings: z.record(z.string(), z.string()),
  identity: z.string(),
  locking: z.boolean(),
  encrypted: z.boolean(),
  localStateFiles: z.array(z.string()),
  sharedWith: z.array(z.string()),
  risks: z.array(iacStateRiskSchema)
});

export const iacStateReportSchema = z.object({
  state: z.object({
    projectId: z.string(),
    root: z.string().nullable(),
    roots: z.array(iacStateRootSchema),
    recommendation: z.object({ provider: z.string(), backend: z.string(), reason: z.string() }).nullable()
  })
});

/** state list e workspace list: endereços e nomes, nunca atributos. */
export const iacStateReadSchema = z.object({
  state: z.object({
    succeeded: z.boolean(),
    workspace: z.string().nullable(),
    workspaces: z.array(z.string()),
    addresses: z.array(z.string()),
    error: z.string().nullable()
  })
});

const iacReviewedChangeSchema = z.object({
  path: z.string(),
  op: z.string(),
  diff: z.string(),
  truncated: z.boolean(),
  beforeHash: z.string().nullable(),
  afterHash: z.string()
});

export const iacBootstrapPlanSchema = z.object({
  plan: z.object({
    planId: z.string(),
    projectId: z.string(),
    provider: z.string(),
    folder: z.string(),
    changes: z.array(iacReviewedChangeSchema),
    backendBlock: z.string(),
    notes: z.array(z.string())
  })
});

/** O desenho em dados: a IA (ou a pessoa) preenche, o motor confere e gera os arquivos com modelos fixos. */
export const iacBlueprintSchema = z.object({
  provider: z.string(),
  project: z.string(),
  region: z.string(),
  account: z.string().nullable(),
  profile: z.string().nullable(),
  network: z.object({
    vpcId: z.string(),
    privateSubnets: z.array(z.string()),
    publicSubnets: z.array(z.string())
  }),
  service: z
    .object({
      name: z.string(),
      port: z.number(),
      healthPath: z.string(),
      cpu: z.number(),
      memory: z.number(),
      desiredCount: z.number(),
      certificateArn: z.string(),
      publicIp: z.boolean()
    })
    .nullable(),
  database: z
    .object({
      engine: z.string(),
      engineVersion: z.string(),
      instanceClass: z.string(),
      storageGb: z.number(),
      multiAz: z.boolean()
    })
    .nullable(),
  queues: z.array(z.string()),
  alarmEmail: z.string().nullable(),
  backend: z.object({ bucket: z.string(), region: z.string() }).nullable()
});

export const iacBlueprintCheckSchema = z.object({
  id: z.string(),
  description: z.string(),
  passed: z.boolean(),
  detail: z.string().nullable()
});

export const iacArchitectDraftSchema = z.object({
  draft: z.object({
    blueprint: iacBlueprintSchema.nullable(),
    notes: z
      .object({
        services: z.array(z.object({ name: z.string(), reason: z.string() })),
        risks: z.array(z.string()),
        alternatives: z.array(z.string()),
        assumptions: z.array(z.string())
      })
      .nullable(),
    checks: z.array(iacBlueprintCheckSchema),
    valid: z.boolean(),
    attempts: z.number(),
    inputTokens: z.number(),
    outputTokens: z.number()
  })
});

export const iacGeneratePlanSchema = z.object({
  plan: z.object({
    planId: z.string(),
    projectId: z.string(),
    root: z.string(),
    changes: z.array(iacReviewedChangeSchema),
    checks: z.array(iacBlueprintCheckSchema),
    costs: z.array(z.object({ resource: z.string(), billing: z.string() })),
    notes: z.array(z.string())
  })
});

export const iacGenerateSaveSchema = z.object({ runId: z.string() });

export const iacBootstrapSaveSchema = z.object({ runId: z.string() });
export const iacUnlockSchema = z.object({ unlocked: z.boolean() });

export type IacStateReport = z.infer<typeof iacStateReportSchema>['state'];
export type IacStateRoot = z.infer<typeof iacStateRootSchema>;
export type IacStateRead = z.infer<typeof iacStateReadSchema>['state'];
export type IacBootstrapPlan = z.infer<typeof iacBootstrapPlanSchema>['plan'];
export type IacBlueprint = z.infer<typeof iacBlueprintSchema>;
export type IacBlueprintCheck = z.infer<typeof iacBlueprintCheckSchema>;
export type IacArchitectDraft = z.infer<typeof iacArchitectDraftSchema>['draft'];
export type IacGeneratePlan = z.infer<typeof iacGeneratePlanSchema>['plan'];
