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
  available: z.boolean(),
  /** Política da edição do build: a comercial não instala o Terraform pelo Spock (D-02). */
  managedInstall: z.boolean().default(true),
  externalUse: z.boolean().default(true),
  note: z.string().nullable().default(null)
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
  message: z.string(),
  edition: z.string().default('development')
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
  /** A versão já está na pasta do Spock: instalar não baixa nada. Decidir pelo texto dos impactos quebraria com a redação. */
  alreadyInstalled: z.boolean(),
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

/** Alterado ou apagado na nuvem por fora da ferramenta; só os nomes dos atributos, nunca os valores. */
export const iacDriftedResourceSchema = z.object({
  address: z.string(),
  type: z.string(),
  change: z.enum(['changed', 'deleted']),
  attributes: z.array(z.string())
});

/** Recurso cobrado que o plano muda, com só os atributos de preço; "?" é valor conhecido só depois do apply. */
export const iacBillableSchema = z.object({
  address: z.string(),
  type: z.string(),
  action: z.string(),
  before: z.record(z.string(), z.string()).nullable(),
  after: z.record(z.string(), z.string()).nullable()
});

/** Recurso que existe na conta e não está no state, de um tipo que o projeto já gerencia. */
export const iacUnmanagedResourceSchema = z.object({
  type: z.string(),
  capability: z.string(),
  id: z.string(),
  name: z.string().nullable()
});

export const iacOperationSchema = z.object({
  id: z.string(),
  projectId: z.string(),
  kind: z.enum(['validate', 'plan', 'apply', 'drift']),
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
  lockId: z.string().nullable(),
  drift: z.array(iacDriftedResourceSchema).nullable(),
  billables: z.array(iacBillableSchema).nullable(),
  unmanaged: z.array(iacUnmanagedResourceSchema).nullable(),
  unmanagedNotes: z.array(z.string()).nullable()
});

/** O que o motor exige para aplicar o plano agora; ausente quando o plano não pode ser aplicado (vencido, superado...). */
export const iacApplyRequirementsSchema = z.object({
  planId: z.string(),
  destructive: z.number(),
  risks: z.number(),
  workspaceConfirmation: z.string().nullable(),
  expiresAt: z.string()
});

export const iacListedOperationSchema = iacOperationSchema.extend({
  applyRequirements: iacApplyRequirementsSchema.nullable()
});

export const iacOperationResultSchema = z.object({ operation: iacOperationSchema });
export const iacOperationListSchema = z.object({ operations: z.array(iacListedOperationSchema) });
export const iacLayoutSchema = z.object({
  layout: z.object({ projectId: z.string(), root: z.string().nullable(), folders: z.array(z.string()) })
});

export type IacOperation = z.infer<typeof iacOperationSchema>;
export type IacListedOperation = z.infer<typeof iacListedOperationSchema>;
export type IacPlanSummary = z.infer<typeof iacPlanSummarySchema>;
export type IacApplyRequirements = z.infer<typeof iacApplyRequirementsSchema>;
export type IacDriftedResource = z.infer<typeof iacDriftedResourceSchema>;

/** Uma linha da estimativa: fixa (quantidade e valor no mês) ou por uso (só o preço da unidade). */
export const iacCostLineSchema = z.object({
  resource: z.string(),
  basis: z.string(),
  quantity: z.number().nullable(),
  unit: z.string(),
  unitPrice: z.number().nullable(),
  monthly: z.number().nullable(),
  priceDescription: z.string().nullable()
});

/** Estimativa com os preços públicos sob demanda do provedor; nunca é a fatura. */
export const iacCostEstimateSchema = z.object({
  estimate: z.object({
    currency: z.string(),
    region: z.string(),
    source: z.string(),
    pricedAt: z.string(),
    monthlyFixed: z.number(),
    complete: z.boolean(),
    fixed: z.array(iacCostLineSchema),
    usageBased: z.array(iacCostLineSchema),
    assumptions: z.array(z.string())
  })
});

export type IacCostLine = z.infer<typeof iacCostLineSchema>;

/** Custo fixo mensal de antes e de depois do plano, por recurso; nunca é a fatura. */
export const iacPlanCostSchema = z.object({
  estimate: z.object({
    operationId: z.string(),
    planId: z.string(),
    currency: z.string(),
    region: z.string(),
    source: z.string(),
    pricedAt: z.string(),
    monthlyBefore: z.number(),
    monthlyAfter: z.number(),
    monthlyDelta: z.number(),
    complete: z.boolean(),
    lines: z.array(
      z.object({
        address: z.string(),
        type: z.string(),
        action: z.string(),
        detail: z.string(),
        before: z.number().nullable(),
        after: z.number().nullable(),
        problem: z.string().nullable()
      })
    ),
    uncovered: z.array(z.string()),
    assumptions: z.array(z.string())
  })
});

export type IacPlanCost = z.infer<typeof iacPlanCostSchema>['estimate'];

/** Verificação de drift automática do projeto: só roda com o app aberto e só lê a nuvem. */
export const iacDriftScheduleSchema = z.object({
  schedule: z.object({
    schedule: z
      .object({
        projectId: z.string(),
        enabled: z.boolean(),
        intervalHours: z.number(),
        folder: z.string().nullable(),
        workspace: z.string().nullable(),
        connectionId: z.string().nullable(),
        changedAt: z.string(),
        lastRunAt: z.string().nullable(),
        lastOperationId: z.string().nullable(),
        lastError: z.string().nullable()
      })
      .nullable(),
    nextRunAt: z.string().nullable(),
    intervals: z.array(z.number())
  })
});

export type IacDriftScheduleView = z.infer<typeof iacDriftScheduleSchema>['schedule'];
export type IacUnmanagedResource = z.infer<typeof iacUnmanagedResourceSchema>;
export type IacCostEstimate = z.infer<typeof iacCostEstimateSchema>['estimate'];
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
    publicSubnets: z.array(z.string()),
    privateDnsZone: z.string().nullable().optional()
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
  backend: z
    .object({
      bucket: z.string(),
      region: z.string(),
      resourceGroup: z.string().nullable().optional(),
      container: z.string().nullable().optional()
    })
    .nullable()
});

export const iacBlueprintCheckSchema = z.object({
  id: z.string(),
  description: z.string(),
  passed: z.boolean(),
  detail: z.string().nullable()
});

const iacArchitectureNotesSchema = z.object({
  services: z.array(z.object({ name: z.string(), reason: z.string() })),
  risks: z.array(z.string()),
  alternatives: z.array(z.string()),
  assumptions: z.array(z.string())
});

export const iacArchitectDraftSchema = z.object({
  draft: z.object({
    blueprint: iacBlueprintSchema.nullable(),
    notes: iacArchitectureNotesSchema.nullable(),
    checks: z.array(iacBlueprintCheckSchema),
    valid: z.boolean(),
    attempts: z.number(),
    inputTokens: z.number(),
    outputTokens: z.number()
  })
});

/** O desenho que o agente da sessão gravou no projeto, conferido pelo motor sem chamar modelo; null sem arquivo. */
export const iacArchitectProposalSchema = z.object({
  proposal: z
    .object({
      path: z.string(),
      blueprint: iacBlueprintSchema.nullable(),
      notes: iacArchitectureNotesSchema.nullable(),
      checks: z.array(iacBlueprintCheckSchema),
      valid: z.boolean()
    })
    .nullable()
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
export type IacArchitectProposal = NonNullable<z.infer<typeof iacArchitectProposalSchema>['proposal']>;
export type IacGeneratePlan = z.infer<typeof iacGeneratePlanSchema>['plan'];
