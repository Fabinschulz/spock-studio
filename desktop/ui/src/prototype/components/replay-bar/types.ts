import type { Contract } from '@/prototype/types/journey';
import type { Replay } from '@/prototype/types/replay';

export interface ReplayBarProps {
  replay: Replay;
  contract: Contract;
  onContract: (contract: Contract) => void;
}
