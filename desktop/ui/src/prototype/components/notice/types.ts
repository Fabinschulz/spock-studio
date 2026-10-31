import type { ReactNode } from 'react';

export interface NoticeProps {
  icon: ReactNode;
  warn?: boolean;
  action?: ReactNode;
  children: ReactNode;
}
