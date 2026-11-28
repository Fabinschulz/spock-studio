import { z } from 'zod';

export const reviewSeveritySchema = z.enum(['critical', 'high', 'medium', 'low', 'informational']);

/** O quanto o motor prova: confirmado (o padrão já é o defeito), heurística (depende do contexto) ou hipótese (depende do que está fora do diff). */
export const reviewConfidenceSchema = z.enum(['confirmed', 'heuristic', 'hypothesis']);

export const reviewVerdictSchema = z.enum([
  'approved',
  'approved_with_warnings',
  'rejected',
  'inconclusive',
  'not_run'
]);

export const reviewFindingSchema = z.object({
  id: z.string(),
  ruleId: z.string(),
  reviewId: z.string(),
  projectId: z.string(),
  path: z.string(),
  startLine: z.number().nullable(),
  endLine: z.number().nullable(),
  onChangedLines: z.boolean(),
  category: z.string(),
  severity: reviewSeveritySchema,
  confidence: reviewConfidenceSchema,
  title: z.string(),
  description: z.string(),
  /** O trecho nunca traz o valor de um segredo. */
  evidence: z.object({
    excerpt: z.string(),
    relation: z.string(),
    origin: z.string(),
    symbol: z.string().nullable(),
    contextConsulted: z.array(z.string())
  }),
  expectedBehavior: z.string(),
  actualBehavior: z.string(),
  suggestedCorrection: z.string(),
  relatedRequirement: z.string().nullable(),
  relatedArchitectureRule: z.string().nullable(),
  source: z.string(),
  detector: z.string(),
  status: z.string(),
  createdAt: z.string()
});

export const reviewJudgeSchema = z.object({
  id: z.string(),
  name: z.string(),
  verdict: reviewVerdictSchema,
  reason: z.string(),
  criteria: z.array(z.string()),
  findingIds: z.array(z.string()),
  deterministic: z.boolean()
});

export const reviewFileSchema = z.object({
  path: z.string(),
  oldPath: z.string().nullable(),
  status: z.string(),
  added: z.number(),
  removed: z.number(),
  analyzed: z.boolean(),
  skippedReason: z.string().nullable()
});

export const reviewCountsSchema = z.object({
  files: z.number(),
  filesAnalyzed: z.number(),
  findings: z.number(),
  confirmed: z.number(),
  needsCheck: z.number(),
  recommendations: z.number(),
  bySeverity: z.record(z.string(), z.number())
});

export const modelRunSchema = z.object({
  purpose: z.string(),
  state: z.enum(['used', 'failed', 'skipped']),
  model: z.string().nullable(),
  modelVersion: z.string().nullable(),
  promptHash: z.string().nullable(),
  inputTokens: z.number(),
  outputTokens: z.number(),
  accepted: z.number(),
  discarded: z.number(),
  failureCode: z.string().nullable()
});

export const requirementCheckSchema = z.object({
  index: z.number(),
  text: z.string(),
  status: z.enum(['met', 'partial', 'not_met', 'unknown']),
  path: z.string().nullable(),
  line: z.number().nullable(),
  excerpt: z.string().nullable(),
  note: z.string().nullable()
});

export const gateStateSchema = z.enum(['passed', 'failed', 'blocked', 'not_run', 'timed_out', 'cancelled']);

export const gateResultSchema = z.object({
  id: z.string(),
  kind: z.string(),
  tool: z.string(),
  command: z.string(),
  directory: z.string(),
  origin: z.string(),
  state: gateStateSchema,
  exitCode: z.number().nullable(),
  durationMs: z.number(),
  summary: z.string(),
  logTail: z.string().nullable()
});

export const reviewSchema = z.object({
  contract: z.string(),
  id: z.string(),
  projectId: z.string(),
  mode: z.enum(['working_tree', 'branch']),
  branch: z.string().nullable(),
  base: z.string().nullable(),
  head: z.string().nullable(),
  headCommit: z.string().nullable(),
  createdAt: z.string(),
  finishedAt: z.string(),
  verdict: reviewVerdictSchema,
  summary: z.string(),
  counts: reviewCountsSchema,
  files: z.array(reviewFileSchema),
  findings: z.array(reviewFindingSchema),
  judges: z.array(reviewJudgeSchema),
  rulesApplied: z.array(z.string()),
  contextConsulted: z.array(z.string()),
  limitations: z.array(z.string()),
  nextActions: z.array(z.string()),
  /** Só existem quando a pessoa pediu o modelo, os critérios ou as verificações; nulos nas revisões antigas. */
  modelRuns: z.array(modelRunSchema).nullish(),
  requirements: z.array(requirementCheckSchema).nullish(),
  gates: z.array(gateResultSchema).nullish()
});

export const findingDecisionSchema = z.object({
  findingId: z.string(),
  decision: z.enum(['confirmed', 'dismissed_false_positive', 'accepted_risk', 'fix_requested']),
  justification: z.string().nullable(),
  decidedAt: z.string(),
  reviewId: z.string()
});

export const deliveryDecisionSchema = z.object({
  reviewId: z.string(),
  decision: z.enum(['approved', 'rejected']),
  justification: z.string(),
  decidedAt: z.string()
});

/** O veredito do motor não muda com as decisões: elas só dizem o que ainda bloqueia e se a pessoa assinou a entrega. */
export const reviewStatusSchema = z.object({
  engineVerdict: reviewVerdictSchema,
  openBlocking: z.number(),
  openNeedsCheck: z.number(),
  readyForDelivery: z.boolean(),
  delivery: deliveryDecisionSchema.nullable()
});

export const reviewResponseSchema = z.object({
  review: reviewSchema,
  decisions: z.array(findingDecisionSchema),
  status: reviewStatusSchema
});

export const gateCandidateSchema = z.object({
  id: z.string(),
  kind: z.string(),
  tool: z.string(),
  command: z.string(),
  directory: z.string(),
  origin: z.string()
});

export const gatesListSchema = z.object({ gates: z.array(gateCandidateSchema), modelAvailable: z.boolean() });

export const correctionBriefSchema = z.object({
  brief: z.object({
    reviewId: z.string(),
    attempt: z.number(),
    maxAttempts: z.number(),
    stopped: z.boolean(),
    items: z.array(z.object({ findingId: z.string(), title: z.string(), location: z.string(), attempt: z.number() })),
    constraints: z.array(z.string()),
    text: z.string()
  })
});

export const reviewSummarySchema = z.object({
  id: z.string(),
  mode: z.enum(['working_tree', 'branch']),
  branch: z.string().nullable(),
  base: z.string().nullable(),
  head: z.string().nullable(),
  createdAt: z.string(),
  verdict: reviewVerdictSchema,
  summary: z.string(),
  counts: reviewCountsSchema
});

export const reviewListSchema = z.object({ reviews: z.array(reviewSummarySchema) });

export type ReviewFinding = z.infer<typeof reviewFindingSchema>;
export type ReviewJudge = z.infer<typeof reviewJudgeSchema>;
export type Review = z.infer<typeof reviewSchema>;
export type ReviewSummary = z.infer<typeof reviewSummarySchema>;
export type ReviewVerdict = z.infer<typeof reviewVerdictSchema>;
export type ReviewView = z.infer<typeof reviewResponseSchema>;
export type FindingDecision = z.infer<typeof findingDecisionSchema>;
export type GateCandidate = z.infer<typeof gateCandidateSchema>;
export type GateResult = z.infer<typeof gateResultSchema>;
export type CorrectionBrief = z.infer<typeof correctionBriefSchema>['brief'];
