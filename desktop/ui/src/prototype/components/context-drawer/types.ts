import type { ReactNode } from 'react';

export interface ContextDrawerProps {
  title: string;
  overlay: boolean;
  onClose: () => void;
  children: ReactNode;
}
