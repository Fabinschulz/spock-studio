import type { EngineClient } from '@/shared/api';
import {
  agentPolicySchema,
  discoverySchema,
  harnessCatalogSchema,
  harnessRevisionsSchema,
  harnessStatusSchema,
  mcpInventorySchema,
  profileResultSchema,
  projectKnowledgeSchema,
  runsSchema,
  type AgentPolicy,
  type Discovery,
  type HarnessCatalog,
  type HarnessRevisions,
  type HarnessStatus,
  type McpInventory,
  type ProjectKnowledge,
  type RunSummary,
  type TechnologyProfile
} from '@/shared/schema';

export function readDiscovery(client: EngineClient, projectId: string): Promise<Discovery> {
  return client.request('project.discover', { projectId }, discoverySchema);
}

export async function readRuns(client: EngineClient, projectId: string): Promise<RunSummary[]> {
  const { runs } = await client.request('harness.runs', { projectId }, runsSchema);
  return runs;
}

export async function readProfile(client: EngineClient, projectId: string): Promise<TechnologyProfile | null> {
  const { profile } = await client.request('engineering.profile', { projectId }, profileResultSchema);
  return profile;
}

export async function refreshProfile(client: EngineClient, projectId: string): Promise<TechnologyProfile> {
  const { profile } = await client.request('engineering.analyze', { projectId }, profileResultSchema);
  if (!profile) throw new Error('O motor não devolveu o perfil.');
  return profile;
}

/** O índice só é consultado se a inteligência local já estiver ligada. */
export function readKnowledge(client: EngineClient, projectId: string): Promise<ProjectKnowledge> {
  return client.request('engineering.knowledge', { projectId }, projectKnowledgeSchema);
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
