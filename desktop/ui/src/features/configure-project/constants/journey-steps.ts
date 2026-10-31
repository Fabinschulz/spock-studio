import type { JourneyStepId } from '../types/journey';

export const journeySteps = [
  { id: 'project', label: 'Projeto' },
  { id: 'model', label: 'Modelo e ambiente' },
  { id: 'plan', label: 'Plano' },
  { id: 'review', label: 'Revisão' },
  { id: 'run', label: 'Aplicação' },
  { id: 'result', label: 'Resultado' }
] as const satisfies readonly { id: JourneyStepId; label: string }[];
