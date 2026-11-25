import { Disclosure, StatusChip } from '@/shared/components/ui';
import type { EvalResult, RunResult } from '@/shared/schema';
import { plural } from '@/shared/utils';

import { evalStatusLabel, evalStatusTone } from '../../domain/labels';
import styles from './validation-list.module.scss';

type Report = NonNullable<RunResult['validation']>;

const OTHER_STATUSES: Record<Exclude<EvalResult['status'], 'passed'>, [string, string]> = {
  failed: ['reprovado', 'reprovados'],
  blocked: ['bloqueado', 'bloqueados'],
  not_run: ['não executado', 'não executados']
};

export function validationSummary(results: readonly EvalResult[]): string {
  const count = (status: EvalResult['status']) => results.filter((result) => result.status === status).length;
  const rest = (Object.keys(OTHER_STATUSES) as (keyof typeof OTHER_STATUSES)[])
    .filter((status) => count(status) > 0)
    .map((status) => plural(count(status), ...OTHER_STATUSES[status]));
  return [`${count('passed')} de ${plural(results.length, 'aprovado', 'aprovados')}`, ...rest].join(' · ');
}

/** O que impede a verificação de estar certa já aparece aberto; o que passou fica recolhido. */
function needsAttention(result: EvalResult): boolean {
  return result.status === 'failed' || result.status === 'blocked';
}

/** O que pede atenção vem primeiro, na ordem do motor; as aprovadas ficam numa linha que se abre. */
export function ValidationList({ report }: { report: Report }) {
  const problems = report.results.filter((result) => result.status !== 'passed');
  const passed = report.results.filter((result) => result.status === 'passed');

  return (
    <section aria-labelledby="validacao-titulo" className={styles.section}>
      <h3 id="validacao-titulo" className={styles.title}>
        Verificações
      </h3>
      <p className={styles.summary}>{validationSummary(report.results)}</p>
      {problems.length > 0 ? <EvalRows results={problems} /> : null}
      {passed.length > 0 ? (
        <Disclosure summary={plural(passed.length, 'aprovada', 'aprovadas')}>
          <EvalRows results={passed} />
        </Disclosure>
      ) : null}
    </section>
  );
}

function EvalRows({ results }: { results: readonly EvalResult[] }) {
  return (
    <ul role="list" className={styles.list}>
      {results.map((result) => (
        <li key={result.id}>
          <EvalRow result={result} />
        </li>
      ))}
    </ul>
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
