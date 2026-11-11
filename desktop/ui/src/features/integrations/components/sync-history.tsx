import { errorMessage } from '@/shared/api';
import { StatusChip } from '@/shared/components';
import { useAsyncResource } from '@/shared/hooks';
import type { SyncExecution } from '@/shared/schema';

import { formatEngineDate } from '../domain/format-date';
import { SYNC_STATUS, syncCounts } from '../domain/labels';
import styles from '../integrations-page.module.scss';

/** As últimas sincronizações de um vínculo. Só é montado quando a pessoa abre o histórico. */
export function SyncHistory({ load }: { load: () => Promise<SyncExecution[]> }) {
  const history = useAsyncResource(load);

  if (history.status === 'loading') return <p role="status">Lendo o histórico…</p>;
  if (history.status === 'error')
    return <p role="alert">{errorMessage(history.error, 'Não foi possível ler o histórico.')}</p>;
  if (history.value.length === 0) return <p>Nenhuma sincronização ainda.</p>;

  return (
    <ul className={styles.history}>
      {history.value.map((sync) => (
        <li key={sync.id}>
          <StatusChip tone={SYNC_STATUS[sync.status].tone}>{SYNC_STATUS[sync.status].label}</StatusChip>{' '}
          {formatEngineDate(sync.startedAt)} · {sync.incremental ? 'incremental' : 'completa'} · {syncCounts(sync)}
          {sync.error ? <span className={styles.error}> · {sync.error.message}</span> : null}
        </li>
      ))}
    </ul>
  );
}
