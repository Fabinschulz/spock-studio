import type { ReactNode } from 'react';

import type { StepIssue } from '@/shared/components/ui';

import type { JourneyStepId } from '../../types/journey';

export interface JourneyFrameProps {
  step: JourneyStepId;
  issue?: StepIssue;
  panel?: ReactNode;
  live?: boolean;
  /** Volta a uma etapa já concluída pelo stepper. Ausente quando voltar não faz sentido (execução em andamento ou concluída). */
  onSelectStep?: (step: JourneyStepId) => void;
  children: ReactNode;
}
