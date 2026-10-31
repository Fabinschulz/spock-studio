import type { OperationView } from '@/prototype/types/operation';

export interface OperationTimelineProps {
  view: OperationView;
  clock: number;
  proposed: boolean;
}
