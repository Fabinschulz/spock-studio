import type { ReactNode } from 'react';

import { cn } from '@/shared/utils';

import styles from './StatusChip.module.scss';

export type Tone = 'neutral' | 'success' | 'warning' | 'danger' | 'info';

interface StatusChipProps {
  tone?: Tone;
  children: ReactNode;
  className?: string;
}

/** Estado em texto + cor: a cor reforça, nunca é a única informação. */
export function StatusChip({ tone = 'neutral', children, className }: StatusChipProps) {
  return <span className={cn(styles.chip, styles[tone], className)}>{children}</span>;
}
