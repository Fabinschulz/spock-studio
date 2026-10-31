import type { ReactNode } from 'react';

export interface SectionProps {
  title: string;
  aside?: ReactNode;
  children: ReactNode;
  id?: string;
}
