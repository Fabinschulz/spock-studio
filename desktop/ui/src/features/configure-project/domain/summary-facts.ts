import type { SummaryFact, SummaryInput } from '../types/summary';
import { aiEnvironmentNames } from './ai-environments';

const STEPS_WITH_APPROVALS = ['review', 'run', 'result'];

export function buildSummaryFacts({
  step,
  projectName,
  aiEnvironment,
  plan,
  approvalCount
}: SummaryInput): SummaryFact[] {
  return [
    ['Projeto', projectName],
    ['Modelo', 'Mariliz'],
    ['Ambiente de IA', aiEnvironment ? (aiEnvironmentNames[aiEnvironment] ?? aiEnvironment) : 'a escolher'],
    ['Plano', plan.status === 'ready' ? `${plan.data.changes.length} alterações` : 'ainda não gerado'],
    ['Aprovados', STEPS_WITH_APPROVALS.includes(step) ? `${approvalCount}` : '—']
  ];
}
