import type { ReactNode } from 'react';

export interface StageProps {
  bar: ReactNode;
  panel?: ReactNode;
  panelTitle?: string;
  live?: boolean;
  actions?: ReactNode;
  actionsNote?: ReactNode;
  children: ReactNode;
}
