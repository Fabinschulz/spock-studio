import type { EngineClient } from '@/shared/api';
import {
  discoverySchema,
  harnessCatalogSchema,
  harnessRevisionsSchema,
  harnessStatusSchema,
  mcpInventorySchema,
  profileResultSchema,
  projectKnowledgeSchema,
  runsSchema,
  type Discovery,
  type HarnessCatalog,
  type HarnessRevisions,
  type HarnessStatus,
  type McpInventory,
  type ProjectKnowledge,
  type RunSummary,
  type TechnologyProfile
} from '@/shared/schema';

/** O que a tela Projeto pede ao motor: o que a análise encontrou e as execuções já feitas. */
export function readDiscovery(client: EngineClient, projectId: string): Promise<Discovery> {
  return client.request('project.discover', { projectId }, discoverySchema);
}

export async function readRuns(client: EngineClient, projectId: string): Promise<RunSummary[]> {
  const { runs } = await client.request('harness.runs', { projectId }, runsSchema);
  return runs;
}

/** O perfil salvo, ou nulo quando o projeto ainda não tem um. */
export async function readProfile(client: EngineClient, projectId: string): Promise<TechnologyProfile | null> {
  const { profile } = await client.request('engineering.profile', { projectId }, profileResultSchema);
  return profile;
}

/** Roda a análise de novo e guarda o perfil; o histórico ganha o que mudou. */
export async function refreshProfile(client: EngineClient, projectId: string): Promise<TechnologyProfile> {
  const { profile } = await client.request('engineering.analyze', { projectId }, profileResultSchema);
  if (!profile) throw new Error('O motor não devolveu o perfil.');
  return profile;
}

/** O conhecimento do projeto como o índice o recebe; o índice só é consultado se o runtime de IA já estiver ligado. */
export function readKnowledge(client: EngineClient, projectId: string): Promise<ProjectKnowledge> {
  return client.request('engineering.knowledge', { projectId }, projectKnowledgeSchema);
}

/** O Harness aplicado, conferido contra o disco. Só leitura. */
export function readHarness(client: EngineClient, projectId: string): Promise<HarnessStatus> {
  return client.request('engineering.harness', { projectId }, harnessStatusSchema);
}

/** Os servidores MCP que o `.mcp.json` do projeto declara, e onde há credencial escrita no arquivo. Só leitura. */
export function readMcp(client: EngineClient, projectId: string): Promise<McpInventory> {
  return client.request('engineering.mcp', { projectId }, mcpInventorySchema);
}

/** O catálogo do Harness. Sem projeto, só as fichas; com projeto, também o que está aplicado nele. Só leitura. */
export function readCatalog(client: EngineClient, projectId: string | null): Promise<HarnessCatalog> {
  return client.request('engineering.catalog', projectId === null ? {} : { projectId }, harnessCatalogSchema);
}

/** As revisões do Harness, montadas dos diários de aplicação e reversão. Só leitura. */
export function readRevisions(client: EngineClient, projectId: string): Promise<HarnessRevisions> {
  return client.request('engineering.revisions', { projectId }, harnessRevisionsSchema);
}
