import type { ConflictResolution } from '@/shared/schema';

export type StepSlug = 'projeto' | 'modelo' | 'plano' | 'revisao' | 'aplicacao' | 'resultado';
export type Contract = 'current' | 'proposed';
export type Analysis = 'idle' | 'loading' | 'ready';
export type ForcedState = 'analisando' | 'analisado' | 'gerando-plano';

export interface StepDefinition {
  slug: StepSlug;
  id: string;
  label: string;
}

export type Resolutions = Record<string, ConflictResolution>;

export interface ConfigureState {
  analysis: Analysis;
  env: string | null;
  planning: boolean;
  approved: string[];
  commands: string[];
  resolutions: Resolutions;
}

export interface ConfigureActions {
  analyze: () => void;
  generatePlan: () => void;
  setEnv: (id: string) => void;
  toggleChange: (id: string) => void;
  toggleCommand: (id: string) => void;
  resolve: (id: string, value: ConflictResolution) => void;
}
