import screen from '@/prototype/styles/screen.module.scss';

import type { StepHeaderProps } from './types';

export function StepHeader({ title, lead }: StepHeaderProps) {
  return (
    <header className={screen.head}>
      <h1 className={screen.title}>{title}</h1>
      <p className={screen.lead}>{lead}</p>
    </header>
  );
}
