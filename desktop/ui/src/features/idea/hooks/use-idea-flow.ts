import { errorMessage } from '@/shared/api';
import { IDEA_STAGES, type IdeaDraft, type IdeaPlan, type IdeaSaveResult, type IdeaStage } from '@/shared/schema';
import { useCallback, useState } from 'react';

import type { ApprovedIdea, IdeaApi } from '../service/idea-api';

export type IdeaBusy = 'drafting' | 'planning' | 'saving' | 'reverting' | null;

export interface IdeaFlowState {
  approved: ApprovedIdea;
  drafts: Partial<Record<IdeaStage, IdeaDraft>>;
  plan: IdeaPlan | null;
  saved: IdeaSaveResult | null;
  reverted: boolean;
  busy: IdeaBusy;
  error: string | null;
}

// Uma etapa nova precisa somar ao que já estava, então ela pode devolver uma função do estado atual.
type Changes = Partial<IdeaFlowState> | ((current: IdeaFlowState) => Partial<IdeaFlowState>);

const EMPTY: IdeaFlowState = {
  approved: { idea: '' },
  drafts: {},
  plan: null,
  saved: null,
  reverted: false,
  busy: null,
  error: null
};

/** A primeira etapa ainda não aprovada; `null` quando as três já foram. */
export function nextStage(approved: ApprovedIdea): IdeaStage | null {
  return IDEA_STAGES.find((stage) => !approved[stage]) ?? null;
}

/**
 * Uma etapa por vez: só a próxima pode ser gerada, e aprovar só vale para o que passou nas verificações do motor. Mudar a
 * ideia depois de aprovar começa tudo de novo, porque as etapas seguintes partiram dela.
 */
export function useIdeaFlow(api: IdeaApi, projectId: string) {
  const [state, setState] = useState<IdeaFlowState>(EMPTY);

  const run = useCallback(async (busy: IdeaBusy, work: () => Promise<Changes>, fallback: string) => {
    setState((current) => ({ ...current, busy, error: null }));
    try {
      const changes = await work();
      setState((current) => ({
        ...current,
        ...(typeof changes === 'function' ? changes(current) : changes),
        busy: null
      }));
    } catch (cause) {
      setState((current) => ({ ...current, busy: null, error: errorMessage(cause, fallback) }));
    }
  }, []);

  const draft = useCallback(
    (stage: IdeaStage, approved: ApprovedIdea) =>
      run(
        'drafting',
        async () => {
          const result = await api.draft(projectId, stage, approved);
          return (current: IdeaFlowState) => ({ approved, drafts: { ...current.drafts, [stage]: result } });
        },
        'Não foi possível gerar esta etapa.'
      ),
    [api, projectId, run]
  );

  const approve = useCallback((stage: IdeaStage) => {
    setState((current) => {
      const artifact = current.drafts[stage];
      if (!artifact?.valid || !artifact.artifact) return current;
      return { ...current, approved: { ...current.approved, [stage]: artifact.artifact } };
    });
  }, []);

  const review = useCallback(
    () =>
      run(
        'planning',
        async () => ({ plan: await api.plan(projectId, state.approved) }),
        'Não foi possível montar os documentos.'
      ),
    [api, projectId, run, state.approved]
  );

  const save = useCallback(
    (plan: IdeaPlan) =>
      run(
        'saving',
        async () => {
          const snapshot = await api.save(projectId, state.approved, plan.planId);
          if (snapshot.result?.outcome !== 'completed') throw new Error(snapshot.result?.error ?? 'sem resultado');
          return { saved: snapshot.result, plan: null };
        },
        'Não foi possível gravar os documentos.'
      ),
    [api, projectId, run, state.approved]
  );

  const revert = useCallback(
    (saved: IdeaSaveResult) =>
      run(
        'reverting',
        async () => {
          await api.revert(projectId, saved.runId);
          return { saved: null, reverted: true };
        },
        'Não foi possível desfazer a gravação.'
      ),
    [api, projectId, run]
  );

  const restart = useCallback(() => setState(EMPTY), []);
  const cancelReview = useCallback(() => setState((current) => ({ ...current, plan: null })), []);

  return { state, draft, approve, review, save, revert, restart, cancelReview };
}
