import type { ConfigureActions, ConfigureState, Contract, StepSlug } from '@/prototype/types/journey';
import type { OperationView } from '@/prototype/types/operation';
import type { Replay } from '@/prototype/types/replay';

export interface StepBodyProps {
  slug: StepSlug;
  state: ConfigureState;
  actions: ConfigureActions;
  contract: Contract;
  onContract: (contract: Contract) => void;
  operation: OperationView;
  applyReplay: Replay;
}
