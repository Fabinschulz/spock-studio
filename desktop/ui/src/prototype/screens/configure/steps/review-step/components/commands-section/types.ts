import type { PlannedCommand } from '@/shared/schema';

export interface CommandsSectionProps {
  commands: PlannedCommand[];
  approved: string[];
  onToggle: (id: string) => void;
}
