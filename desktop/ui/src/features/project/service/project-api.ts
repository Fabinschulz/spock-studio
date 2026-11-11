import type { EngineClient } from '@/shared/api';
import {
  discoverySchema,
  profileResultSchema,
  projectKnowledgeSchema,
  runsSchema,
  type Discovery,
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
