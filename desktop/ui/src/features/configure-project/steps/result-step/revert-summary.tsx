import { Alert } from '@synthra.io/ui-kit';

import { runOutcomeLabel } from '@/shared/utils';

import { EvidenceDisclosure, StatusChip } from '@/shared/components/ui';
import type { RunResult } from '@/shared/schema';

import styles from '../../components/panels.module.scss';

export function RevertSummary({ result }: { result: RunResult }) {
  return (
    <div className={styles.card}>
      <h3 className={styles.cardTitle}>
        Reversão{' '}
        <StatusChip tone={result.outcome === 'completed' ? 'success' : 'warning'}>
          {runOutcomeLabel[result.outcome]}
        </StatusChip>
      </h3>
      {result.error ? <Alert severity="error" description={`${result.error.code}: ${result.error.message}`} /> : null}
      <span className={styles.muted}>
        {result.verified
          ? 'Conferido: os arquivos restaurados voltaram ao estado anterior.'
          : 'Os arquivos restaurados não foram todos conferidos.'}
      </span>
      {result.reverted ? (
        <EvidenceDisclosure summary={`Restaurados (${result.reverted.length})`} items={result.reverted} />
      ) : null}
      {result.conflicts && result.conflicts.length > 0 ? (
        <Alert
          severity="warning"
          title="Mantidos como estão"
          description={
            <EvidenceDisclosure summary={`Arquivos (${result.conflicts.length})`} items={result.conflicts} />
          }
        />
      ) : null}
    </div>
  );
}
