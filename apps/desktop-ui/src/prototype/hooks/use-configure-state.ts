import { useState } from 'react';

import { plan } from '@/prototype/data/fixtures';
import { defaultApproved, defaultResolutions } from '@/prototype/domain/default-approvals';
import { toggleId } from '@/prototype/domain/toggle-id';
import type { ConfigureActions, ConfigureState, ForcedState, StepSlug } from '@/prototype/types/journey';

interface Options {
  stepIndex: number;
  forced: ForcedState | null;
  go: (slug: StepSlug) => void;
}

// Os atrasos simulam as chamadas únicas do motor (project.discover e harness.plan), que não emitem progresso.
const ANALYSIS_DELAY_MS = 900;
const PLAN_DELAY_MS = 1100;

// Abrir direto numa etapa parte com as anteriores resolvidas.
function initialState(stepIndex: number, forced: ForcedState | null): ConfigureState {
  const analyzed = stepIndex > 0 || forced === 'analisado';
  return {
    analysis: forced === 'analisando' ? 'loading' : analyzed ? 'ready' : 'idle',
    env: stepIndex > 1 || forced === 'gerando-plano' ? 'claude-code' : null,
    planning: forced === 'gerando-plano',
    approved: defaultApproved(plan),
    commands: [],
    resolutions: defaultResolutions(plan)
  };
}

export function useConfigureState({ stepIndex, forced, go }: Options): [ConfigureState, ConfigureActions] {
  const [state, setState] = useState(() => initialState(stepIndex, forced));
  const patch = (changes: Partial<ConfigureState>) => setState((current) => ({ ...current, ...changes }));

  const actions: ConfigureActions = {
    analyze: () => {
      patch({ analysis: 'loading' });
      window.setTimeout(() => patch({ analysis: 'ready' }), ANALYSIS_DELAY_MS);
    },
    generatePlan: () => {
      patch({ planning: true });
      window.setTimeout(() => {
        patch({ planning: false });
        go('plano');
      }, PLAN_DELAY_MS);
    },
    setEnv: (env) => patch({ env }),
    toggleChange: (id) => setState((current) => ({ ...current, approved: toggleId(current.approved, id) })),
    toggleCommand: (id) => setState((current) => ({ ...current, commands: toggleId(current.commands, id) })),
    resolve: (id, value) =>
      setState((current) => ({ ...current, resolutions: { ...current.resolutions, [id]: value } }))
  };

  return [state, actions];
}
