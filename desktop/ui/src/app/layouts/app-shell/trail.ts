import type { Crumb } from '@/shared/components';
import { executionPattern, paths } from '@/shared/routing';

import { ENGINEERING_GROUP, MANAGEMENT_GROUP, PLATFORM_GROUP, type NavGroup } from './navigation';

const EXECUTION_PREFIX = executionPattern.split('/:')[0] ?? '';

const GROUPS: readonly NavGroup[] = [ENGINEERING_GROUP, MANAGEMENT_GROUP, PLATFORM_GROUP];

// A Plataforma não tem tela própria: o nome dela na trilha é só texto.
const GROUP_HOME: Record<string, string> = {
  [ENGINEERING_GROUP.id]: paths.project,
  [MANAGEMENT_GROUP.id]: paths.management
};

function groupCrumb(group: NavGroup): Crumb {
  const to = GROUP_HOME[group.id];
  return to ? { label: group.label ?? '', to } : { label: group.label ?? '' };
}

// A execução abre-se a partir de uma lista e não está no menu, então a trilha dela passa pela lista.
function executionTrail(): Crumb[] {
  const runs = PLATFORM_GROUP.items.find((item) => item.to === paths.runs);
  return [groupCrumb(ENGINEERING_GROUP), { label: runs?.label ?? '', to: paths.runs }, { label: 'Execução' }];
}

export function trailOf(pathname: string): Crumb[] {
  if (pathname.startsWith(`${EXECUTION_PREFIX}/`)) return executionTrail();

  for (const group of GROUPS) {
    const item = group.items.find((entry) => entry.to === pathname);
    if (item) return [groupCrumb(group), { label: item.label }];
  }
  return [];
}
