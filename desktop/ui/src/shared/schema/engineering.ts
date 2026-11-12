import { z } from 'zod';

/** O perfil tecnológico do projeto, como o motor o guarda. Categoria e cobertura do Harness vêm prontas do motor. */
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

export const profileResultSchema = z.object({ profile: technologyProfileSchema.nullable() });

export type TechnologyProfile = z.infer<typeof technologyProfileSchema>;
export type ProfileChange = TechnologyProfile['history'][number];

/** O conhecimento do projeto como o índice o recebe. Só caminhos e tamanhos: o conteúdo não sai do motor. */
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
  orphaned: z.number()
});

export type ProjectKnowledge = z.infer<typeof projectKnowledgeSchema>;

const harnessStateSchema = z.enum(['intact', 'kept', 'modified', 'outOfScope', 'missing']);

/** O Harness aplicado, lido de mariliz.lock e conferido contra o disco pelo motor. */
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
      files: z.array(z.object({ path: z.string(), mode: z.string(), state: harnessStateSchema }))
    })
  ),
  excluded: z.array(z.object({ id: z.string(), label: z.string(), manageable: z.boolean() })),
  intact: z.number(),
  kept: z.number(),
  modified: z.number(),
  missing: z.number()
});

export type HarnessStatus = z.infer<typeof harnessStatusSchema>;

/** Uma revisão do Harness: aplicação ou reversão que mudou arquivos, com o que mudou por componente. */
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

export type HarnessRevision = z.infer<typeof harnessRevisionSchema>;
export type HarnessRevisions = z.infer<typeof harnessRevisionsSchema>;
