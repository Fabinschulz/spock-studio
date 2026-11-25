import { plural } from '@/shared/utils';

import type { SummaryFact, SummaryInput } from '../types/summary';
import { aiEnvironmentNames } from './ai-environments';

const STEPS_WITH_APPROVALS = ['review', 'run', 'result'];

export function buildSummaryFacts({
  step,
  projectName,
  aiEnvironments,
  plan,
  approvalCount
}: SummaryInput): SummaryFact[] {
  const environments = aiEnvironments.map((id) => aiEnvironmentNames[id] ?? id).join(', ');
  return [
    ['Projeto', projectName],
    ['Modelo', 'Spock'],
    [aiEnvironments.length > 1 ? 'Ambientes de IA' : 'Ambiente de IA', environments || 'a escolher'],
    [
      'Plano',
      plan.status === 'ready' ? plural(plan.data.changes.length, 'alteração', 'alterações') : 'ainda não gerado'
    ],
    ['Aprovados', STEPS_WITH_APPROVALS.includes(step) ? `${approvalCount}` : 'não se aplica']
  ];
}
