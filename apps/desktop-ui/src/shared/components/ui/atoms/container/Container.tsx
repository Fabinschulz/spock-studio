import type { ElementType, ReactNode } from 'react';

import { cn } from '@/shared/utils';

import styles from './Container.module.scss';

interface ContainerProps {
  as?: ElementType;
  className?: string;
  children: ReactNode;
}

/** Coluna de conteúdo da janela: largura máxima e respiro lateral consistentes. */
export function Container({ as: Tag = 'div', className, children }: ContainerProps) {
  return <Tag className={cn(styles.container, className)}>{children}</Tag>;
}
