import type { ConfigurationPlan, ConflictResolution } from '@/shared/schema';

import type { Resolutions } from '@/prototype/types/journey';

export interface ReviewStepProps {
  plan: ConfigurationPlan;
  approved: string[];
  commands: string[];
  resolutions: Resolutions;
  onToggle: (id: string) => void;
  onToggleCommand: (id: string) => void;
  onResolve: (id: string, value: ConflictResolution) => void;
}
