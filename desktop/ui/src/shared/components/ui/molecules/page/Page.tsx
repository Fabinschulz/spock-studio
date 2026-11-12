import { cn } from '@/shared/utils';
import type { ReactNode } from 'react';
import { PageHeader, type Crumb } from '../page-header';
import styles from './Page.module.scss';

interface PageProps {
  title: ReactNode;
  /** Uma frase de contexto sob o título. A explicação longa vai para uma seção, não para aqui. */
  description?: ReactNode;
  breadcrumb?: readonly Crumb[];
  /**
   * `text` (padrão) limita a largura a uma coluna de leitura. `wide` usa a largura disponível, para listas e tabelas
   * que ganham com ela.
   */
  width?: 'text' | 'wide';
  children?: ReactNode;
}

/**
 * O esqueleto de toda tela: cabeçalho (título, contexto, trilha) e o conteúdo em coluna, com o mesmo espaçamento. As
 * telas deixam de repetir `.page`, `.title` e `.lead` e passam a compor `Page` com `PageSection`.
 */
export function Page({ title, description, breadcrumb, width = 'text', children }: PageProps) {
  return (
    <div className={cn(styles.page, width === 'wide' && styles.wide)}>
      <PageHeader title={title} description={description} breadcrumb={breadcrumb} />
      {children}
    </div>
  );
}
