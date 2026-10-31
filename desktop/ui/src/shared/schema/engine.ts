import { z } from 'zod';

/**
 * Contrato das respostas do motor para a jornada "Configurar projeto". Espelha o C#
 * (Mariliz.Core e Mariliz.Application) e é conferido contra as fixtures golden de contracts.
 * A interface valida toda resposta antes de usá-la: um formato inesperado vira erro, não tela quebrada.
 */

const confidence = z.enum(['high', 'medium', 'low']);
const evidence = z.object({ path: z.string(), detail: z.string().nullable() });

export const stackFindingSchema = z.object({
  id: z.string(),
  name: z.string(),
  root: z.string(),
  version: z.string().nullable(),
  confidence,
  evidence: z.array(evidence),
  notes: z.array(z.string())
});

export const toolingFindingSchema = z.object({
  kind: z.string(),
  name: z.string(),
  command: z.string().nullable(),
  evidence: z.array(evidence)
});

export const aiEnvironmentSchema = z.object({
  id: z.string(),
  name: z.string(),
  supported: z.boolean(),
  confidence,
  evidence: z.array(evidence)
});

const inventoryItem = z.object({
  name: z.string(),
  path: z.string(),
  managed: z.boolean(),
  issue: z.string().nullable()
});

export const specificationStatusSchema = z.object({
  state: z.enum(['not_installed', 'installed', 'drifted', 'invalid']),
  version: z.string().nullable(),
  lockedHash: z.string().nullable(),
  computedHash: z.string().nullable(),
  versionTested: z.boolean(),
  problems: z.array(z.string())
});

export const discoveryWarningSchema = z.object({ code: z.string(), message: z.string(), paths: z.array(z.string()) });

export const discoverySchema = z.object({
  projectId: z.string(),
  projectName: z.string(),
  stacks: z.array(stackFindingSchema),
  tooling: z.array(toolingFindingSchema),
  aiEnvironments: z.array(aiEnvironmentSchema),
  inventory: z.object({
    agents: z.array(inventoryItem),
    skills: z.array(inventoryItem),
    hooks: z.array(inventoryItem),
    rules: z.array(inventoryItem),
    mcpServers: z.array(inventoryItem),
    evals: z.array(inventoryItem)
  }),
  specification: specificationStatusSchema,
  git: z.object({
    isRepository: z.boolean(),
    hasUncommittedChanges: z.boolean().nullable(),
    changedPaths: z.array(z.string())
  }),
  tools: z.object({
    git: z.string().nullable(),
    node: z.string().nullable(),
    python: z.string().nullable(),
    pythonCommand: z.string().nullable()
  }),
  hasDeclaredConfig: z.boolean(),
  warnings: z.array(discoveryWarningSchema)
});

const componentKind = z.enum(['specification', 'agent', 'skill', 'hook', 'rule', 'tool', 'eval', 'config']);
const changeAction = z.enum(['create', 'update', 'preserve', 'remove', 'conflict']);

export const componentDecisionSchema = z.object({
  kind: componentKind,
  componentId: z.string(),
  decision: z.string(),
  justification: z.string(),
  origin: z.string(),
  evidence: z.array(z.string())
});

export const fileChangeSchema = z.object({
  id: z.string(),
  kind: componentKind,
  componentId: z.string(),
  path: z.string(),
  action: changeAction,
  reason: z.string(),
  origin: z.string(),
  beforeHash: z.string().nullable(),
  afterHash: z.string().nullable(),
  destructive: z.boolean(),
  conflictReason: z.string().nullable(),
  diff: z.string().nullable(),
  diffTruncated: z.boolean()
});

export const plannedCommandSchema = z.object({
  id: z.string(),
  description: z.string(),
  executable: z.string(),
  arguments: z.array(z.string()),
  effects: z.array(z.enum(['process', 'network', 'write_local'])),
  destructive: z.boolean(),
  affectedPaths: z.array(z.string())
});

export const limitationSchema = z.object({ code: z.string(), message: z.string() });

export const observationSchema = z.object({
  kind: z.enum(['fact', 'inference', 'hypothesis', 'unverified']),
  text: z.string(),
  evidence: z.array(z.string())
});

export const planSchema = z.object({
  planId: z.string(),
  projectId: z.string(),
  aiEnvironment: z.string(),
  implementationModel: z.string(),
  proposalOrigin: z.string(),
  decisions: z.array(componentDecisionSchema),
  changes: z.array(fileChangeSchema),
  commands: z.array(plannedCommandSchema),
  limitations: z.array(limitationSchema),
  observations: z.array(observationSchema),
  risks: z.array(z.string()),
  validations: z.array(z.string()),
  isEmpty: z.boolean()
});

export const evalResultSchema = z.object({
  id: z.string(),
  name: z.string(),
  property: z.string(),
  kind: z.enum(['deterministic', 'model']),
  required: z.boolean(),
  status: z.enum(['passed', 'failed', 'blocked', 'not_run']),
  criteria: z.string(),
  evidence: z.array(z.string()),
  reason: z.string().nullable(),
  limitations: z.array(z.string())
});

export const validationSchema = z.object({
  outcome: z.enum(['passed', 'passed_with_limitations', 'failed']),
  results: z.array(evalResultSchema)
});

const runStep = z.object({ id: z.string(), title: z.string(), status: z.string(), detail: z.string().nullable() });

export const runOutcomeSchema = z.enum([
  'completed',
  'completed_with_limitations',
  'failed',
  'requires_intervention',
  'cancelled'
]);

export const runResultSchema = z.object({
  runId: z.string(),
  projectId: z.string(),
  kind: z.enum(['apply', 'validate', 'revert']),
  planId: z.string().optional(),
  revertsRunId: z.string().optional(),
  outcome: runOutcomeSchema,
  changesApplied: z.boolean().optional(),
  rolledBack: z.boolean().optional(),
  files: z
    .object({
      created: z.array(z.string()),
      updated: z.array(z.string()),
      removed: z.array(z.string()),
      preserved: z.array(z.string()),
      kept: z.array(z.string()),
      notApplied: z.array(z.string()),
      commandEffects: z.array(z.string())
    })
    .optional(),
  lockWritten: z.boolean().optional(),
  validation: validationSchema.optional(),
  pending: z.array(z.string()).optional(),
  limitations: z.array(limitationSchema).optional(),
  risks: z.array(z.string()).optional(),
  reverted: z.array(z.string()).optional(),
  conflicts: z.array(z.string()).optional(),
  verified: z.boolean().optional(),
  error: z.object({ code: z.string(), message: z.string() }).optional(),
  steps: z.array(runStep).optional(),
  rollbackAvailable: z.boolean().optional()
});

export const runEventSchema = z.object({
  v: z.literal(1),
  kind: z.literal('event'),
  runId: z.string(),
  type: z.string(),
  seq: z.number(),
  data: z.record(z.string(), z.unknown())
});

export const runSnapshotSchema = z.object({
  runId: z.string(),
  projectId: z.string(),
  kind: z.string(),
  state: z.string(),
  lastSeq: z.number(),
  events: z.array(z.object({ seq: z.number(), type: z.string(), data: z.record(z.string(), z.unknown()) })),
  result: runResultSchema.nullable()
});

export const runStartedSchema = z.object({ runId: z.string() });

/** Corpo de `input` em workflow.start (engineering.request.v1). Stacks vêm do contexto do motor. */
export const workflowStartInputSchema = z.object({
  goal: z.string().min(1),
  impact: z.boolean().optional(),
  requiresApproval: z.boolean().optional()
});

/** Parâmetros de workflow.start — espelha contracts/schemas/ai/workflow.start.params.schema.json */
export const workflowStartParamsSchema = z
  .object({
    projectId: z.string().min(1),
    input: workflowStartInputSchema.optional()
  })
  .strict();

export const workflowStartResultSchema = runStartedSchema;
export const runsListSchema = z.object({
  runs: z.array(
    z.object({
      runId: z.string(),
      kind: z.string(),
      state: z.string(),
      startedAt: z.string(),
      changesApplied: z.boolean(),
      reverted: z.boolean()
    })
  )
});
export const cancelSchema = z.object({ cancelled: z.boolean() });

export const modelStatusSchema = z.object({
  configured: z.boolean(),
  provider: z.string()
});

/** Decisão da pessoa para um arquivo em conflito: manter o dela ou substituir (com backup). */
export type ConflictResolution = 'keep' | 'overwrite';

export type Discovery = z.infer<typeof discoverySchema>;
export type StackFinding = z.infer<typeof stackFindingSchema>;
export type ConfigurationPlan = z.infer<typeof planSchema>;
export type FileChange = z.infer<typeof fileChangeSchema>;
export type PlannedCommand = z.infer<typeof plannedCommandSchema>;
export type ComponentDecision = z.infer<typeof componentDecisionSchema>;
export type EvalResult = z.infer<typeof evalResultSchema>;
export type RunResult = z.infer<typeof runResultSchema>;
export type RunEvent = z.infer<typeof runEventSchema>;
export type RunSnapshot = z.infer<typeof runSnapshotSchema>;
export type RunSummary = z.infer<typeof runsListSchema>['runs'][number];
export type WorkflowStartInput = z.infer<typeof workflowStartInputSchema>;
export type WorkflowStartParams = z.infer<typeof workflowStartParamsSchema>;
