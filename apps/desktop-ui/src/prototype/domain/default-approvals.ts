import type { ConfigurationPlan } from '@/shared/schema';

import type { Resolutions } from '@/prototype/types/journey';

export const defaultApproved = (plan: ConfigurationPlan): string[] =>
  plan.changes.filter((c) => c.action === 'create' || c.action === 'update').map((c) => c.id);

export const defaultResolutions = (plan: ConfigurationPlan): Resolutions =>
  Object.fromEntries(plan.changes.filter((c) => c.action === 'conflict').map((c) => [c.id, 'keep' as const]));
