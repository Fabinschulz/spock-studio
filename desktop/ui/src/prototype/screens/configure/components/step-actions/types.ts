import type { ConfigureActions, ConfigureState, StepSlug } from '@/prototype/types/journey';

export interface StepActionsProps {
  slug: StepSlug;
  state: ConfigureState;
  actions: ConfigureActions;
  approvedCount: number;
  operationFinished: boolean;
  go: (slug: StepSlug) => void;
  onConclude: () => void;
}
