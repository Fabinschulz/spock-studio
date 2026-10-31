import type { ReactNode } from 'react';

import type { StepIssue } from '@/shared/components/ui';

import type { JourneyStepId } from '../../types/journey';

export interface JourneyFrameProps {
  step: JourneyStepId;
  issue?: StepIssue;
  panel?: ReactNode;
  live?: boolean;
  children: ReactNode;
}
