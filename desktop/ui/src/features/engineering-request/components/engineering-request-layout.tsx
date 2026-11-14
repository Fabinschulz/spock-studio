import { Page } from '@/shared/components/ui';
import type { ReactNode } from 'react';

interface EngineeringRequestLayoutProps {
  description: string;
  children: ReactNode;
}

export function EngineeringRequestLayout({ description, children }: EngineeringRequestLayoutProps) {
  return (
    <Page title="Pedido de engenharia" description={description}>
      {children}
    </Page>
  );
}
