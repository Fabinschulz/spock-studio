import { z } from 'zod';

/** Categoria e cobertura do Harness vêm prontas do motor. */
export const technologyProfileSchema = z.object({
  projectId: z.string(),
  analyzedAt: z.string(),
  technologies: z.array(
    z.object({
      id: z.string(),
      name: z.string(),
      category: z.string(),
      versions: z.array(z.string()),
      roots: z.array(z.string()),
      confidence: z.enum(['high', 'medium', 'low']),
      evidence: z.array(z.string())
    })
  ),
  tooling: z.array(z.object({ kind: z.string(), name: z.string(), command: z.string().nullable() })),
  aiEnvironments: z.array(z.string()),
  connections: z.array(z.object({ role: z.string(), provider: z.string(), resource: z.string() })),
  harness: z.array(
    z.object({ technology: z.string(), ruleId: z.string().nullable(), covered: z.boolean(), detail: z.string() })
  ),
  history: z.array(
    z.object({
      at: z.string(),
      kind: z.enum(['created', 'added', 'removed', 'changed']),
      subject: z.string(),
      from: z.string().nullable(),
      to: z.string().nullable()
    })
  )
});

/** Situação de suporte de cada pasta, calculada pelo motor a partir do catálogo de versões embutido. */
export const compatibilitySchema = z.object({
  catalogGeneratedAt: z.string(),
  catalogStale: z.boolean(),
  modules: z.array(
    z.object({
      root: z.string(),
      technologyId: z.string(),
      name: z.string(),
      version: z.string().nullable(),
      origin: z.enum(['stack', 'runtime', 'framework']),
      status: z.enum([
        'current',
        'supported',
        'maintenance',
        'end-of-life',
        'preview',
        'partial',
        'unknown',
        'incompatible'
      ]),
      statusLabel: z.string(),
      eol: z.string().nullable(),
      newestStable: z.string().nullable(),
      confirmed: z.boolean(),
      reason: z.string()
    })
  )
});

export const profileResultSchema = z.object({
  profile: technologyProfileSchema.nullable(),
  compatibility: compatibilitySchema.nullable().optional()
});

export type TechnologyProfile = z.infer<typeof technologyProfileSchema>;
export type Compatibility = z.infer<typeof compatibilitySchema>;
export type ProfileResult = z.infer<typeof profileResultSchema>;
export type ProfileChange = TechnologyProfile['history'][number];

/** Sem `authorized`, nada vai e `documents` é zero. Só contagens e rótulos: o texto dos itens não sai do motor. */
export const knowledgeExternalSourceSchema = z.object({
  integrationId: z.string(),
  providerName: z.string(),
  resourceName: z.string(),
  authorized: z.boolean(),
  dataKinds: z.array(z.string()),
  items: z.number(),
  documents: z.number(),
  /** Itens autorizados que ficaram de fora: acima do limite ou com possível segredo no texto. */
  withheld: z.number(),
  indexed: z.number(),
  pending: z.number(),
  outdated: z.number()
});

/** Só contagens: o texto das decisões não sai do motor por esta resposta. */
export const knowledgeMemorySchema = z.object({
  documents: z.number(),
  indexed: z.number(),
  pending: z.number(),
  outdated: z.number()
});

/** Só caminhos e tamanhos: o conteúdo não sai do motor. */
export const projectKnowledgeSchema = z.object({
  projectId: z.string(),
  inspectedAt: z.string(),
  include: z.array(z.string()),
  exclude: z.array(z.string()),
  allowed: z.number(),
  files: z.array(
    z.object({ path: z.string(), chars: z.number(), state: z.enum(['indexed', 'outdated', 'pending', 'unknown']) })
  ),
  excluded: z.array(
    z.object({ reason: z.string(), label: z.string(), count: z.number(), examples: z.array(z.string()) })
  ),
  indexState: z.enum(['read', 'runtimeStopped', 'runtimeUnavailable', 'failed']),
  indexed: z.number(),
  outdated: z.number(),
  pending: z.number(),
  orphaned: z.number(),
  external: z.array(knowledgeExternalSourceSchema),
  memory: knowledgeMemorySchema
});

const architectureFindingSchema = z.object({
  code: z.string(),
  severity: z.enum(['warning', 'info']),
  title: z.string(),
  evidence: z.array(z.object({ path: z.string(), detail: z.string().nullable() }))
});

/**
 * A análise determinística do código (W2-2) e a avaliação que o agente da sessão gravou no projeto (W4-1), conferida pelo
 * motor sem modelo. `assessment` é null enquanto o agente não gravou nada.
 */
const planCheckSchema = z.object({
  id: z.string(),
  description: z.string(),
  passed: z.boolean(),
  detail: z.string().nullable()
});

export const projectArchitectureSchema = z.object({
  architecture: z.object({
    report: z.object({
      projectId: z.string(),
      analyzedAt: z.string(),
      dotnetProjects: z.array(
        z.object({
          path: z.string(),
          name: z.string(),
          layer: z.string(),
          references: z.array(z.string()),
          frameworks: z.array(z.string()),
          tested: z.boolean()
        })
      ),
      frontends: z.array(
        z.object({
          root: z.string(),
          source: z.string(),
          folders: z.array(z.string()),
          features: z.array(z.string()),
          featureDependencies: z.array(z.string()),
          layerLint: z.boolean()
        })
      ),
      findings: z.array(architectureFindingSchema),
      ownImplementations: z.array(z.object({ name: z.string(), folder: z.string(), replaces: z.string() }))
    }),
    assessment: z
      .object({
        path: z.string(),
        summary: z.string().nullable(),
        conventions: z.array(z.object({ title: z.string(), files: z.array(z.string()) })),
        divergences: z.array(
          z.object({
            id: z.string(),
            title: z.string(),
            classification: z.string(),
            description: z.string(),
            files: z.array(z.string()),
            references: z.array(z.string()),
            findings: z.array(z.string()),
            recommendation: z.string()
          })
        ),
        checks: z.array(planCheckSchema),
        valid: z.boolean()
      })
      .nullable(),
    assessmentPath: z.string()
  })
});

const modernizationStepSchema = z.object({
  id: z.string(),
  title: z.string(),
  kind: z.string(),
  change: z.string(),
  divergences: z.array(z.string()),
  files: z.array(z.string()),
  dependsOn: z.array(z.string()),
  undo: z.string(),
  verification: z.string(),
  risk: z.string(),
  cost: z.string().nullable(),
  coexistence: z.string().nullable(),
  fingerprint: z.string()
});

/** Resposta de modernization.plan e modernization.decide: o plano do agente conferido e a decisão da pessoa por passo. */
export const projectModernizationSchema = z.object({
  modernization: z.object({
    assessment: z
      .object({
        valid: z.boolean(),
        divergences: z.array(z.object({ id: z.string(), title: z.string(), classification: z.string() }))
      })
      .nullable(),
    plan: z
      .object({
        path: z.string(),
        goal: z.string().nullable(),
        characterization: z.array(z.object({ behavior: z.string(), files: z.array(z.string()), test: z.string() })),
        steps: z.array(modernizationStepSchema),
        checks: z.array(planCheckSchema),
        valid: z.boolean()
      })
      .nullable(),
    planPath: z.string(),
    steps: z.array(
      z.object({
        stepId: z.string(),
        status: z.enum(['pending', 'approved', 'rejected', 'changed']),
        decisionId: z.string().nullable(),
        recordedAt: z.string().nullable()
      })
    )
  })
});

export type ProjectArchitecture = z.infer<typeof projectArchitectureSchema>['architecture'];
export type ProjectModernization = z.infer<typeof projectModernizationSchema>['modernization'];
export type ModernizationStep = z.infer<typeof modernizationStepSchema>;
export type ArchitectureFinding = z.infer<typeof architectureFindingSchema>;
export type KnowledgeMemory = z.infer<typeof knowledgeMemorySchema>;
export type KnowledgeExternalSource = z.infer<typeof knowledgeExternalSourceSchema>;
export type ProjectKnowledge = z.infer<typeof projectKnowledgeSchema>;

const harnessStateSchema = z.enum(['intact', 'kept', 'modified', 'outOfScope', 'missing']);

export const harnessStatusSchema = z.object({
  projectId: z.string(),
  inspectedAt: z.string(),
  applied: z.boolean(),
  lockReadable: z.boolean(),
  engine: z.string().nullable(),
  aiEnvironment: z.string().nullable(),
  specificationVersion: z.string().nullable(),
  components: z.array(
    z.object({
      id: z.string(),
      label: z.string(),
      kind: z.string(),
      version: z.string(),
      state: harnessStateSchema,
      manageable: z.boolean(),
      files: z.array(z.object({ path: z.string(), mode: z.string(), state: harnessStateSchema })),
      /** Numa regra gerada da pasta Organização: o que o catálogo do motor tem de diferente do que ela registrou. */
      referenceChanges: z
        .array(z.string())
        .nullish()
        .transform((changes) => changes ?? [])
    })
  ),
  excluded: z.array(z.object({ id: z.string(), label: z.string(), manageable: z.boolean() })),
  intact: z.number(),
  kept: z.number(),
  modified: z.number(),
  missing: z.number()
});

export type HarnessStatus = z.infer<typeof harnessStatusSchema>;

export const harnessRevisionSchema = z.object({
  number: z.number(),
  runId: z.string(),
  kind: z.enum(['apply', 'revert']),
  at: z.string(),
  state: z.string(),
  rolledBack: z.boolean(),
  revertedByRunId: z.string().nullable(),
  revertsRunId: z.string().nullable(),
  components: z.array(
    z.object({
      id: z.string(),
      label: z.string(),
      created: z.array(z.string()),
      updated: z.array(z.string()),
      removed: z.array(z.string())
    })
  )
});

export const harnessRevisionsSchema = z.object({
  projectId: z.string(),
  appliedByEngine: z.string().nullable(),
  currentEngine: z.string(),
  revisions: z.array(harnessRevisionSchema)
});

export const harnessToolSchema = z.object({ name: z.string(), plain: z.string() });

export const componentSheetSchema = z.object({
  id: z.string(),
  kind: z.string(),
  title: z.string(),
  version: z.string(),
  whatItDoes: z.string(),
  whenItActs: z.string(),
  reads: z.array(z.string()),
  canChange: z.array(z.string()),
  /** Só agentes: as ferramentas que o próprio arquivo do agente declara. */
  tools: z.array(harnessToolSchema),
  origin: z.string(),
  files: z.array(z.string()),
  manageable: z.boolean()
});

/**
 * `presence` é nulo sem projeto. `state` é o do pior dos arquivos e só existe se o componente está no lock; num excluído,
 * quer dizer que os arquivos ainda estão lá.
 */
export const catalogEntrySchema = z.object({
  sheet: componentSheetSchema,
  presence: z.string().nullable(),
  state: z.string().nullable()
});

export const harnessCatalogSchema = z.object({
  projectId: z.string().nullable(),
  applied: z.boolean().nullable(),
  entries: z.array(catalogEntrySchema)
});

/** Uma variável de ambiente do servidor. `referenced`: o valor vem do ambiente (`${NOME}`), não está escrito no arquivo. */
export const mcpEnvVarSchema = z.object({ name: z.string(), referenced: z.boolean() });

/** Onde há uma credencial escrita no arquivo (`env.NOME`, `args[2]`, `headers.NOME`, `url`). O valor nunca vem do motor. */
export const mcpLiteralSecretSchema = z.object({ where: z.string(), why: z.string() });

/** Um servidor MCP do `.mcp.json`. `runs`: o programa (stdio) ou só o servidor (http e sse). */
export const mcpServerSchema = z.object({
  name: z.string(),
  transport: z.string(),
  runs: z.string(),
  env: z.array(mcpEnvVarSchema),
  literalSecrets: z.array(mcpLiteralSecretSchema)
});

/** `readable` é falso se o `.mcp.json` existe mas não é um JSON válido. */
export const mcpInventorySchema = z.object({
  projectId: z.string(),
  present: z.boolean(),
  readable: z.boolean(),
  servers: z.array(mcpServerSchema)
});

/** Uma regra do hook de política, em palavras simples. `decision`: deny (bloqueia), ask (pergunta antes) ou allow (libera uma exceção). */
export const policyRuleSchema = z.object({ decision: z.string(), scope: z.string(), plain: z.string() });

export const agentPolicySchema = z.object({
  script: z.string(),
  watches: z.array(z.string()),
  rules: z.array(policyRuleSchema),
  limits: z.array(z.string())
});

export type AgentPolicy = z.infer<typeof agentPolicySchema>;
export type PolicyRule = z.infer<typeof policyRuleSchema>;
export type McpServer = z.infer<typeof mcpServerSchema>;
export type McpInventory = z.infer<typeof mcpInventorySchema>;
export type ComponentSheet = z.infer<typeof componentSheetSchema>;
export type CatalogEntry = z.infer<typeof catalogEntrySchema>;
export type HarnessCatalog = z.infer<typeof harnessCatalogSchema>;
export type HarnessRevision = z.infer<typeof harnessRevisionSchema>;
export type HarnessRevisions = z.infer<typeof harnessRevisionsSchema>;
