import { cn } from '@/shared/utils';
import type { ReactNode } from 'react';
import { PageHeader, usePageTrail, type Crumb } from '../page-header';
import { PageTabs, usePageTabs } from '../page-tabs';
import styles from './Page.module.scss';

interface PageProps {
  title: ReactNode;
  /** Uma frase de contexto sob o título. A explicação longa vai para uma seção, não para aqui. */
  description?: ReactNode;
  /** Sem ela, vale a trilha do shell: só uma tela que foge do menu precisa da sua. */
  breadcrumb?: readonly Crumb[];
  actions?: ReactNode;
  /**
   * `text` (padrão) limita a largura a uma coluna de leitura. `wide` usa a largura disponível, para listas e tabelas
   * que ganham com ela.
   */
  width?: 'text' | 'wide';
  children?: ReactNode;
}

export function Page({ title, description, breadcrumb, actions, width = 'text', children }: PageProps) {
  const trail = usePageTrail();
  const tabs = usePageTabs();

  return (
    <div className={cn(styles.page, width === 'wide' && styles.wide)}>
      <PageHeader title={title} description={description} breadcrumb={breadcrumb ?? trail} actions={actions} />
      <PageTabs set={tabs} />
      {children}
    </div>
  );
}
