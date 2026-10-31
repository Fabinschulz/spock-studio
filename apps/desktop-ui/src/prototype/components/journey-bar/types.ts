import type { StepSlug } from '@/prototype/types/journey';

export type JourneyItemState = 'issue' | 'done' | 'current' | 'todo';

export interface JourneyIssue {
  index: number;
  label: string;
}

export interface JourneyBarProps {
  current: number;
  issue?: JourneyIssue;
  onGo: (slug: StepSlug) => void;
}
