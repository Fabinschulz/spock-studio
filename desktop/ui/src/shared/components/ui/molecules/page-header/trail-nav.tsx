import { Breadcrumb } from '@mariliz/ui-kit';

import type { Crumb } from './PageHeader';

export function TrailNav({ trail }: { trail: readonly Crumb[] }) {
  if (trail.length === 0) return null;

  return (
    <Breadcrumb
      aria-label="Trilha de navegação"
      links={trail.map((crumb) => ({ url: crumb.to ?? '', title: crumb.label }))}
    />
  );
}
