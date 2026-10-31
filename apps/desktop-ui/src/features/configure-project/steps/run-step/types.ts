import type { RunView } from '../../types/run';

export interface RunStepProps {
  run: RunView;
  onCancel: () => Promise<void> | void;
  onBack: () => void;
}
