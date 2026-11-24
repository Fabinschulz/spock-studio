import { runUntilDone, type EngineClient } from '@/shared/api';
import {
  ideaDraftSchema,
  ideaPlanSchema,
  ideaSaveSnapshotSchema,
  projectDesignSchema,
  runStartedSchema,
  type IdeaArtifact,
  type IdeaDraft,
  type IdeaPlan,
  type IdeaStage,
  type ProjectDesign
} from '@/shared/schema';
import type { z } from 'zod';

export type IdeaSaveSnapshot = z.infer<typeof ideaSaveSnapshotSchema>;

/** As etapas já aprovadas: cada uma só é mandada depois de passar nas verificações do motor. */
export interface ApprovedIdea {
  idea: string;
  specification?: IdeaArtifact;
  architecture?: IdeaArtifact;
  backlog?: IdeaArtifact;
}

export interface IdeaApi {
  draft(projectId: string, stage: IdeaStage, approved: ApprovedIdea): Promise<IdeaDraft>;
  plan(projectId: string, approved: ApprovedIdea): Promise<IdeaPlan>;
  save(projectId: string, approved: ApprovedIdea, planId: string): Promise<IdeaSaveSnapshot>;
  revert(projectId: string, runId: string): Promise<void>;
  /** Modo Sessão: o que o agente da pessoa gravou em .spock/project-design.json, já conferido pelo motor. */
  readDesign(projectId: string): Promise<ProjectDesign>;
}

export function createIdeaApi(client: EngineClient): IdeaApi {
  return {
    draft: (projectId, stage, approved) =>
      client.request('idea.draft', { projectId, stage, ...approved }, ideaDraftSchema),
    plan: (projectId, approved) => client.request('idea.plan', { projectId, ...approved }, ideaPlanSchema),
    readDesign: async (projectId) =>
      (await client.request('project.design', { projectId }, projectDesignSchema)).design,
    save: async (projectId, approved, planId) => {
      const runId = await runUntilDone(
        client,
        async () => (await client.request('idea.save', { projectId, planId, ...approved }, runStartedSchema)).runId
      );
      return client.request('run.snapshot', { runId }, ideaSaveSnapshotSchema);
    },
    revert: async (projectId, runId) => {
      await runUntilDone(
        client,
        async () => (await client.request('harness.revert', { projectId, runId }, runStartedSchema)).runId
      );
    }
  };
}
