import type { Analysis } from '@/prototype/types/journey';

export interface ProjectStepProps {
  analysis: Analysis;
  onAnalyze: () => void;
}
