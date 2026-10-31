import { Alert } from '@synthra.io/ui-kit';

import type { Loadable } from '../types/journey';

export function PlanError({ plan }: { plan: Loadable<unknown> }) {
  if (plan.status !== 'error') return null;
  return <Alert severity="error" title="Não foi possível gerar o plano" description={plan.message} />;
}
