import type { Crumb, PageTabSet } from '@/shared/components';
import { executionPattern, paths } from '@/shared/routing';

import { AREAS, tabOwner, type NavArea } from './navigation';

const EXECUTION_PREFIX = executionPattern.split('/:')[0] ?? '';

const areaCrumb = (area: NavArea): Crumb => ({ label: area.label, to: area.to });

// A execução abre-se a partir de uma lista e não está no menu, então a trilha dela passa pela lista.
function executionTrail(): Crumb[] {
  const activity = AREAS.find((area) => area.id === 'activity');
  const runs = activity?.items.find((item) => item.to === paths.runs);
  return [
    ...(activity ? [areaCrumb(activity)] : []),
    { label: runs?.label ?? '', to: paths.runs },
    { label: 'Execução' }
  ];
}

export function tabSetOf(pathname: string): PageTabSet | null {
  const owner = tabOwner(pathname);
  return owner?.item.tabs ? { label: owner.item.label, tabs: owner.item.tabs } : null;
}

export function trailOf(pathname: string): Crumb[] {
  if (pathname.startsWith(`${EXECUTION_PREFIX}/`)) return executionTrail();

  const owner = tabOwner(pathname);
  if (owner) return [areaCrumb(owner.area), { label: owner.item.label, to: owner.item.to }, { label: owner.tab.label }];

  for (const area of AREAS) {
    const item = area.items.find((entry) => entry.to === pathname);
    if (item) return [areaCrumb(area), { label: item.label }];
  }
  return [];
}
