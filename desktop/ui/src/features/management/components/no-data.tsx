import { EmptyState, PageSection } from '@/shared/components';
import { pathTo } from '@/shared/routing';
import { Link } from 'react-router';

/** Uma seção sem dado diz o que falta e como ter, em vez de sumir. */
export function NoData({ title, children }: { title: string; children: string }) {
  return (
    <PageSection title={title}>
      <EmptyState>
        {children} Sincronize em <Link to={pathTo('integrations')}>Conexões › Serviços</Link>.
      </EmptyState>
    </PageSection>
  );
}
