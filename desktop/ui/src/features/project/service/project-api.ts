import type { EngineClient } from '@/shared/api';
import { discoverySchema, runsSchema, type Discovery, type RunSummary } from '@/shared/schema';

/** O que a tela Projeto pede ao motor: o que a análise encontrou e as execuções já feitas. */
export function readDiscovery(client: EngineClient, projectId: string): Promise<Discovery> {
  return client.request('project.discover', { projectId }, discoverySchema);
}

export async function readRuns(client: EngineClient, projectId: string): Promise<RunSummary[]> {
  const { runs } = await client.request('harness.runs', { projectId }, runsSchema);
  return runs;
}
