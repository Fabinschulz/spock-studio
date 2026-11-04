import { StatusChip } from '@/shared/components/ui';
import type { EvalResult, RunResult } from '@/shared/schema';

import { evalStatusLabel, evalStatusTone } from '../../domain/labels';
import styles from './validation-list.module.scss';

type Report = NonNullable<RunResult['validation']>;

const EVAL_STATUSES: readonly EvalResult['status'][] = ['passed', 'failed', 'blocked', 'not_run'];

/** "3 de 14 aprovados · 1 reprovado": só os estados que existem, para a frase não listar zeros. */
export function validationSummary(results: readonly EvalResult[]): string {
  const count = (status: EvalResult['status']) => results.filter((result) => result.status === status).length;
  const rest = EVAL_STATUSES.filter((status) => status !== 'passed' && count(status) > 0).map(
    (status) => `${count(status)} ${evalStatusLabel[status].toLowerCase()}`
  );
  return [`${count('passed')} de ${results.length} aprovados`, ...rest].join(' · ');
}

/** O que impede a verificação de estar certa já aparece aberto; o que passou fica recolhido. */
function needsAttention(result: EvalResult): boolean {
  return result.status === 'failed' || result.status === 'blocked';
}

export function ValidationList({ report }: { report: Report }) {
  return (
    <section aria-labelledby="validacao-titulo" className={styles.section}>
      <h3 id="validacao-titulo" className={styles.title}>
        Verificações
      </h3>
      <p className={styles.summary}>{validationSummary(report.results)}</p>
      <ul role="list" className={styles.list}>
        {report.results.map((result) => (
          <li key={result.id}>
            <EvalRow result={result} />
          </li>
        ))}
      </ul>
    </section>
  );
}

function EvalRow({ result }: { result: EvalResult }) {
  return (
    <details className={styles.details} open={needsAttention(result) || undefined}>
      <summary className={styles.row}>
        <span className={styles.name}>{result.name}</span>
        <StatusChip tone={evalStatusTone[result.status]}>{evalStatusLabel[result.status]}</StatusChip>
      </summary>
      <div className={styles.body}>
        <p className={styles.meta}>
          {result.id} · {result.kind === 'model' ? 'juiz por modelo' : 'determinístico'} ·{' '}
          {result.required ? 'obrigatório' : 'opcional'}
        </p>
        <p>{result.property}</p>
        <p className={styles.meta}>Critério: {result.criteria}</p>
        {result.reason ? <p>{result.reason}</p> : null}
        {result.limitations.map((limitation) => (
          <p key={limitation} className={styles.meta}>
            Limite: {limitation}
          </p>
        ))}
        {result.evidence.length > 0 ? (
          <ul role="list" className={styles.evidence}>
            {result.evidence.map((line, index) => (
              // A evidência pode repetir o mesmo texto; a lista é estática, então a posição é a chave.
              <li key={`${index}:${line}`}>{line}</li>
            ))}
          </ul>
        ) : null}
      </div>
    </details>
  );
}
