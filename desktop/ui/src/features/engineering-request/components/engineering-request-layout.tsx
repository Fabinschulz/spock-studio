import { PageHeader } from '@/shared/components/ui';
import { pathTo } from '@/shared/routing';
import type { ReactNode } from 'react';

import styles from '../engineering-request-page.module.scss';

const BREADCRUMB = [{ label: 'Início', to: pathTo('home') }, { label: 'Pedido de engenharia' }] as const;

interface EngineeringRequestLayoutProps {
  description: string;
  children: ReactNode;
}

export function EngineeringRequestLayout({ description, children }: EngineeringRequestLayoutProps) {
  return (
    <section className={styles.page}>
      <PageHeader title="Pedido de engenharia" description={description} breadcrumb={[...BREADCRUMB]} />
      {children}
    </section>
  );
}
