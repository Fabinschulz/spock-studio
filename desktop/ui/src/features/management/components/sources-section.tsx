import { SYNC_STATUS, kindCounts } from '@/features/integrations';
import { PageSection, StatusChip } from '@/shared/components';
import { pathTo } from '@/shared/routing';
import type { Connector, ProjectIntegration } from '@/shared/schema';
import { formatEngineDate } from '@/shared/utils';
import { Link } from 'react-router';

import styles from '../management.module.scss';

export function SourcesSection({
  integrations,
  connectors
}: {
  integrations: ProjectIntegration[];
  connectors: Connector[];
}) {
  return (
    <PageSection title="Fontes de dados">
      <ul className={styles.rows}>
        {integrations.map((integration) => {
          const connector = connectors.find((entry) => entry.provider === integration.provider);
          const kinds = kindCounts(integration, connector);
          return (
            <li key={integration.id} className={styles.row}>
              <span>
                <span className={styles.rowTitle}>{integration.scope.resourceName}</span>
                <span className={styles.muted}>
                  {' '}
                  · {connector?.name ?? integration.provider}
                  {kinds.length > 0 ? ` · ${kinds.join(', ')}` : ' · sem itens sincronizados'}
                  {integration.items.lastSyncedAt ? ` · lido ${formatEngineDate(integration.items.lastSyncedAt)}` : ''}
                </span>
              </span>
              {integration.lastSync ? (
                <StatusChip tone={SYNC_STATUS[integration.lastSync.status].tone}>
                  {SYNC_STATUS[integration.lastSync.status].label}
                </StatusChip>
              ) : null}
            </li>
          );
        })}
      </ul>
      <p className={styles.muted}>
        Conexões, recortes e sincronização ficam em <Link to={pathTo('integrations')}>Conexões › Serviços</Link>.
      </p>
    </PageSection>
  );
}
