import { EmptyState, ResourceView, StatusChip } from '@/shared/components';
import { useAsyncResource } from '@/shared/hooks';
import type { SyncExecution } from '@/shared/schema';

import { formatEngineDate } from '../domain/format-date';
import { SYNC_STATUS, syncCounts } from '../domain/labels';
import styles from '../integrations-page.module.scss';

export function SyncHistory({ load }: { load: () => Promise<SyncExecution[]> }) {
  const history = useAsyncResource(load);

  return (
    <ResourceView resource={history} loading="Lendo o histórico…" fallbackError="Não foi possível ler o histórico.">
      {(syncs) =>
        syncs.length === 0 ? (
          <EmptyState>Nenhuma sincronização ainda.</EmptyState>
        ) : (
          <ul className={styles.history}>
            {syncs.map((sync) => (
              <li key={sync.id}>
                <StatusChip tone={SYNC_STATUS[sync.status].tone}>{SYNC_STATUS[sync.status].label}</StatusChip>{' '}
                {formatEngineDate(sync.startedAt)} · {sync.incremental ? 'incremental' : 'completa'} ·{' '}
                {syncCounts(sync)}
                {sync.error ? <span className={styles.error}> · {sync.error.message}</span> : null}
              </li>
            ))}
          </ul>
        )
      }
    </ResourceView>
  );
}
