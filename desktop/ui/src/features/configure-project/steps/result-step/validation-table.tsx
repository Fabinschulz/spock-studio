import { EvidenceDisclosure, StatusChip } from '@/shared/components/ui';
import type { EvalResult, RunResult } from '@/shared/schema';

import styles from '../../components/panels.module.scss';
import { ScrollableRegion } from '../../components/scrollable-region';
import { evalStatusLabel, evalStatusTone } from '../../domain/labels';

const EVAL_STATUSES: readonly EvalResult['status'][] = ['passed', 'failed', 'blocked', 'not_run'];

export function ValidationTable({ report }: { report: NonNullable<RunResult['validation']> }) {
  const countOf = (status: EvalResult['status']) => report.results.filter((result) => result.status === status).length;

  return (
    <div className={styles.card}>
      <h3 className={styles.cardTitle}>Evals</h3>
      <div className={styles.chips} aria-label="Resumo dos Evals">
        {EVAL_STATUSES.map((status) => (
          <StatusChip key={status} tone={evalStatusTone[status]}>
            {countOf(status)} {evalStatusLabel[status].toLowerCase()}
          </StatusChip>
        ))}
      </div>
      <ScrollableRegion label="Resultados dos Evals">
        <table className={styles.table}>
          <thead>
            <tr>
              <th scope="col">Eval</th>
              <th scope="col">Estado</th>
              <th scope="col">Evidência e limites</th>
            </tr>
          </thead>
          <tbody>
            {report.results.map((result) => (
              <tr key={result.id}>
                <td>
                  <div>{result.name}</div>
                  <div className={styles.mono}>
                    {result.id} · {result.kind === 'model' ? 'juiz por modelo' : 'determinístico'} ·{' '}
                    {result.required ? 'obrigatório' : 'opcional'}
                  </div>
                </td>
                <td>
                  <StatusChip tone={evalStatusTone[result.status]}>{evalStatusLabel[result.status]}</StatusChip>
                </td>
                <td>
                  {result.reason ? <div>{result.reason}</div> : null}
                  {result.limitations.map((limitation) => (
                    <div key={limitation} className={styles.muted}>
                      Limite: {limitation}
                    </div>
                  ))}
                  <EvidenceDisclosure
                    summary={`Critério e evidências (${result.evidence.length})`}
                    items={result.evidence}
                  >
                    <p className={styles.muted}>Critério: {result.criteria}</p>
                  </EvidenceDisclosure>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </ScrollableRegion>
    </div>
  );
}
