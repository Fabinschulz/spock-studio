import type { Contract } from '@/prototype/types/journey';
import type { OperationView } from '@/prototype/types/operation';
import type { Replay } from '@/prototype/types/replay';

export interface OperationStepProps {
  view: OperationView;
  replay: Replay;
  contract: Contract;
  onContract: (contract: Contract) => void;
}
