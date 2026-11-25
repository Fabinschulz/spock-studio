import { InlineNotice } from '@/shared/components/ui';
import type { Loadable } from '../types/journey';

export function PlanError({ plan }: { plan: Loadable<unknown> }) {
  if (plan.status !== 'error') return null;
  return (
    <InlineNotice tone="danger" title="Não foi possível gerar o plano">
      {plan.message}
    </InlineNotice>
  );
}
