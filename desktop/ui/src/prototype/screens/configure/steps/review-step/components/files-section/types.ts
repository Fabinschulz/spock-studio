import type { ConflictResolution, FileChange } from '@/shared/schema';

import type { Resolutions } from '@/prototype/types/journey';

export interface FilesSectionProps {
  changes: FileChange[];
  approved: string[];
  resolutions: Resolutions;
  onToggle: (id: string) => void;
  onResolve: (id: string, value: ConflictResolution) => void;
}
