import type { ConflictResolution } from '@/shared/schema';

export interface ConflictResolutionProps {
  path: string;
  value: ConflictResolution;
  onChange: (value: ConflictResolution) => void;
}
