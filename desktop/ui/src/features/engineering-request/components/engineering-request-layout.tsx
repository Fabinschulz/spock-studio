import { Page } from '@/shared/components/ui';
import { pathTo } from '@/shared/routing';
import type { ReactNode } from 'react';

const BREADCRUMB = [{ label: 'Início', to: pathTo('home') }, { label: 'Pedido de engenharia' }] as const;

interface EngineeringRequestLayoutProps {
  description: string;
  children: ReactNode;
}

export function EngineeringRequestLayout({ description, children }: EngineeringRequestLayoutProps) {
  return (
    <Page title="Pedido de engenharia" description={description} breadcrumb={[...BREADCRUMB]}>
      {children}
    </Page>
  );
}
