import type { EngineClient } from '@/shared/api';
import {
  agentPolicySchema,
  discoverySchema,
  harnessCatalogSchema,
  harnessRevisionsSchema,
  harnessStatusSchema,
  mcpInventorySchema,
  profileResultSchema,
  projectArchitectureSchema,
  projectKnowledgeSchema,
  projectModernizationSchema,
  runsSchema,
  type AgentPolicy,
  type Discovery,
  type HarnessCatalog,
  type HarnessRevisions,
  type HarnessStatus,
  type McpInventory,
  type ProjectArchitecture,
  type ProjectKnowledge,
  type ProjectModernization,
  type RunSummary,
  type ProfileResult
} from '@/shared/schema';

export function readDiscovery(client: EngineClient, projectId: string): Promise<Discovery> {
  return client.request('project.discover', { projectId }, discoverySchema);
}

export async function readRuns(client: EngineClient, projectId: string): Promise<RunSummary[]> {
  const { runs } = await client.request('harness.runs', { projectId }, runsSchema);
  return runs;
}

export function readProfile(client: EngineClient, projectId: string): Promise<ProfileResult> {
  return client.request('engineering.profile', { projectId }, profileResultSchema);
}

export async function refreshProfile(client: EngineClient, projectId: string): Promise<ProfileResult> {
  const result = await client.request('engineering.analyze', { projectId }, profileResultSchema);
  if (!result.profile) throw new Error('O motor não devolveu o perfil.');
  return result;
}

/** O índice só é consultado se a inteligência local já estiver ligada. */
export function readKnowledge(client: EngineClient, projectId: string): Promise<ProjectKnowledge> {
  return client.request('engineering.knowledge', { projectId }, projectKnowledgeSchema);
}

/** Lê o código de novo a cada chamada e confere a avaliação contra essa mesma análise; não chama modelo. */
export async function readArchitecture(client: EngineClient, projectId: string): Promise<ProjectArchitecture> {
  const { architecture } = await client.request('project.architecture', { projectId }, projectArchitectureSchema);
  return architecture;
}

/** O plano que o agente gravou, conferido contra a avaliação atual, com a decisão da pessoa em cada passo. */
export async function readModernization(client: EngineClient, projectId: string): Promise<ProjectModernization> {
  const { modernization } = await client.request('modernization.plan', { projectId }, projectModernizationSchema);
  return modernization;
}

/** Registra a decisão na memória de decisões do projeto; vale só para o texto atual do passo. */
export async function decideModernizationStep(
  client: EngineClient,
  projectId: string,
  stepId: string,
  decision: 'approved' | 'rejected',
  rationale: string
): Promise<ProjectModernization> {
  const { modernization } = await client.request(
    'modernization.decide',
    { projectId, stepId, decision, rationale: rationale.trim() || null },
    projectModernizationSchema
  );
  return modernization;
}
export function readHarness(client: EngineClient, projectId: string): Promise<HarnessStatus> {
  return client.request('engineering.harness', { projectId }, harnessStatusSchema);
}

export function readPolicy(client: EngineClient): Promise<AgentPolicy> {
  return client.request('engineering.policy', {}, agentPolicySchema);
}

export function readMcp(client: EngineClient, projectId: string): Promise<McpInventory> {
  return client.request('engineering.mcp', { projectId }, mcpInventorySchema);
}

export function readCatalog(client: EngineClient, projectId: string | null): Promise<HarnessCatalog> {
  return client.request('engineering.catalog', projectId === null ? {} : { projectId }, harnessCatalogSchema);
}

export function readRevisions(client: EngineClient, projectId: string): Promise<HarnessRevisions> {
  return client.request('engineering.revisions', { projectId }, harnessRevisionsSchema);
}
