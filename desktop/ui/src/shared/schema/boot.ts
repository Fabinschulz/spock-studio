import { z } from 'zod';

/**
 * O Boot: interpreta o pedido, confere o que a capacidade precisa, monta o plano e executa só leitura e preparação; o resto
 * abre a tela que já existe. O registro é do motor; a interface só mostra.
 */
export const bootStepSchema = z.object({
  id: z.string(),
  kind: z.enum(['check', 'context', 'capability', 'handoff']),
  title: z.string(),
  capabilityId: z.string().nullable(),
  method: z.string().nullable(),
  dependsOn: z.array(z.string()),
  operation: z.string(),
  status: z.enum(['pending', 'running', 'completed', 'skipped', 'failed', 'blocked']),
  startedAt: z.string().nullable(),
  finishedAt: z.string().nullable(),
  summary: z.string(),
  /** O que o Spock tirou do pedido para a consulta (período, quantidade), sempre dentro da faixa que o motor aceita. */
  arguments: z.record(z.string(), z.number()),
  argumentNotes: z.array(z.string()),
  evidence: z.array(z.string()),
  errorCode: z.string().nullable()
});

export const bootStateSchema = z.enum([
  'created',
  'interpreting',
  'awaitingClarification',
  'preparing',
  'planned',
  'executing',
  'completed',
  'failed',
  'cancelled',
  'interrupted'
]);

export const bootOutcomeSchema = z.enum([
  'answered',
  'prepared',
  'handoff',
  'needsClarification',
  'needsInput',
  'unsupported',
  'failed'
]);

export const bootAlternativeSchema = z.object({ capabilityId: z.string(), name: z.string(), reason: z.string() });

export const bootResultSchema = z.object({
  outcome: bootOutcomeSchema,
  headline: z.string(),
  done: z.array(z.string()),
  consulted: z.array(z.string()),
  changes: z.array(z.string()),
  validations: z.array(z.string()),
  problems: z.array(z.string()),
  pending: z.array(z.string()),
  nextStep: z.string().nullable(),
  route: z.string().nullable(),
  routeLabel: z.string().nullable(),
  alternatives: z.array(bootAlternativeSchema)
});

export const bootRecordSchema = z.object({
  id: z.string(),
  version: z.number(),
  mode: z.enum(['request', 'project', 'work', 'recovery', 'autonomous']),
  origin: z.string(),
  source: z.string().nullable(),
  projectId: z.string().nullable(),
  stableProjectId: z.string().nullable(),
  state: bootStateSchema,
  autonomy: z.string(),
  request: z.string().nullable(),
  hint: z.string().nullable(),
  intent: z
    .object({
      resolution: z.enum(['resolved', 'needsClarification', 'noMatch']),
      capabilityId: z.string().nullable(),
      // 'text' (léxico), 'explicit' (escolha direta) ou 'model' (segundo estágio, conferido pelo motor contra o catálogo).
      source: z.string(),
      domain: z.string().nullable(),
      candidates: z.array(
        z.object({ capabilityId: z.string(), score: z.number(), confidence: z.number(), matched: z.array(z.string()) })
      ),
      question: z.string().nullable(),
      objective: z.string(),
      modelStage: z.enum(['skipped', 'unavailable', 'failed', 'used'])
    })
    .nullable(),
  // O gasto de modelo do reconhecimento da intenção; nulo quando o modelo não foi chamado.
  modelUsage: z.object({ calls: z.number(), inputTokens: z.number(), outputTokens: z.number() }).nullable(),
  plan: z
    .object({
      objective: z.string(),
      domain: z.string().nullable(),
      capabilities: z.array(z.string()),
      operation: z.string(),
      requiredLevel: z.string(),
      approvals: z.array(z.string()),
      risks: z.array(z.string()),
      successCriteria: z.array(z.string()),
      expectedResult: z.string()
    })
    .nullable(),
  steps: z.array(bootStepSchema),
  checkpoints: z.array(
    z.object({ at: z.string(), state: z.string(), stepId: z.string().nullable(), note: z.string() })
  ),
  result: bootResultSchema.nullable(),
  contextVersion: z.string().nullable(),
  recoversBootId: z.string().nullable(),
  recoveredByBootId: z.string().nullable(),
  createdAt: z.string(),
  updatedAt: z.string(),
  finishedAt: z.string().nullable(),
  errorCode: z.string().nullable()
});

export const bootResponseSchema = z.object({ boot: bootRecordSchema });

export const bootListSchema = z.object({
  boots: z.array(
    z.object({
      id: z.string(),
      mode: z.string(),
      state: bootStateSchema,
      projectId: z.string().nullable(),
      createdAt: z.string(),
      finishedAt: z.string().nullable(),
      recoversBootId: z.string().nullable(),
      recoveredByBootId: z.string().nullable(),
      outcome: bootOutcomeSchema.nullable(),
      headline: z.string().nullable(),
      /** Um Boot interrompido não tem manchete: o objetivo é o que o distingue na lista. */
      objective: z.string().nullable(),
      /** O motor só aceita retomar o interrompido que tem plano e ainda não foi recuperado. */
      resumable: z.boolean(),
      capabilities: z.array(z.string())
    })
  )
});

export const bootCapabilitySchema = z.object({
  id: z.string(),
  name: z.string(),
  description: z.string(),
  domain: z.string(),
  goals: z.array(z.string()),
  operation: z.string(),
  mode: z.enum(['direct', 'handoff', 'composite']),
  route: z.string().nullable(),
  agents: z.array(z.string()),
  skills: z.array(z.string()),
  workflows: z.array(z.string()),
  composes: z.array(z.string()),
  expectedResult: z.string(),
  preconditions: z.array(z.string()),
  parameters: z.array(z.object({ name: z.string(), label: z.string(), min: z.number(), max: z.number() })),
  needs: z.array(z.string()),
  availability: z.enum([
    'available',
    'partial',
    'unavailable',
    'notConfigured',
    'blockedByPermission',
    'dependsOnIntegration',
    'maintenance'
  ]),
  reason: z.string().nullable(),
  verdict: z.enum(['allowed', 'handoffOnly', 'denied']),
  needsApproval: z.boolean(),
  requiredLevel: z.number()
});

export const bootCapabilitiesSchema = z.object({ autonomy: z.number(), capabilities: z.array(bootCapabilitySchema) });

export type BootRecord = z.infer<typeof bootRecordSchema>;
export type BootStep = z.infer<typeof bootStepSchema>;
export type BootResult = z.infer<typeof bootResultSchema>;
export type BootCapability = z.infer<typeof bootCapabilitySchema>;
