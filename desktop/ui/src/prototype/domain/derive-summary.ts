import { AI_ENVIRONMENT_NAMES } from '@/prototype/constants/environments';
import { demoProject } from '@/prototype/data/demo-project';
import type { SummaryFact, SummaryInput } from '@/prototype/types/summary';

export const deriveSummary = ({ env, stepIndex, changeCount, approvedCount }: SummaryInput): SummaryFact[] => [
  ['Projeto', demoProject.name],
  ['Modelo', 'Mariliz'],
  ['Ambiente de IA', env ? (AI_ENVIRONMENT_NAMES[env] ?? env) : 'a escolher'],
  ['Plano', stepIndex >= 2 ? `${changeCount} alterações` : 'ainda não gerado'],
  ['Aprovados', stepIndex >= 3 ? `${approvedCount}` : '—']
];
