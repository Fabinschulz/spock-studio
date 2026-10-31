import type { ConfigurationPlan } from '@/shared/schema';

import type { JourneyState } from '../types/journey';

/** Criar e atualizar vêm marcados; remover, conflito e comandos exigem um gesto da pessoa. */
export function defaultApprovals(plan: ConfigurationPlan) {
  return {
    approvedChanges: plan.changes.filter((c) => c.action === 'create' || c.action === 'update').map((c) => c.id),
    approvedCommands: [] as string[],
    resolutions: Object.fromEntries(
      plan.changes.filter((c) => c.action === 'conflict').map((c) => [c.id, 'keep' as const])
    )
  };
}

export const toggleId = (list: string[], id: string) =>
  list.includes(id) ? list.filter((item) => item !== id) : [...list, id];

/** Substituir um conflito conta como aprovar a mudança. */
export function buildApplyInput(state: JourneyState, projectId: string, plan: ConfigurationPlan) {
  const conflicts = plan.changes.filter((c) => c.action === 'conflict');
  const overwrites = conflicts.filter((c) => state.resolutions[c.id] === 'overwrite').map((c) => c.id);
  const planned = new Set(plan.changes.map((c) => c.id));

  return {
    projectId,
    planId: plan.planId,
    approvedChangeIds: [...new Set([...state.approvedChanges.filter((id) => planned.has(id)), ...overwrites])],
    approvedCommandIds: state.approvedCommands,
    resolutions: Object.fromEntries(conflicts.map((c) => [c.id, state.resolutions[c.id] ?? 'keep'] as const))
  };
}

export function countApprovals(state: JourneyState, plan: ConfigurationPlan): number {
  const input = buildApplyInput(state, '', plan);
  return input.approvedChangeIds.length + input.approvedCommandIds.length;
}
