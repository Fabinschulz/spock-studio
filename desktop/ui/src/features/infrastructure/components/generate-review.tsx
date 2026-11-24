import { pathTo } from '@/shared/routing';
import type { CloudConnection, IacGeneratePlan } from '@/shared/schema';
import { Button } from '@mariliz/ui-kit';
import { Link } from 'react-router';

import styles from '../infrastructure-page.module.scss';
import { LINKED_UNREADABLE } from '../domain/linked-connections';
import type { GenerateFlow } from '../hooks/use-generate-flow';
import { CostEstimateView } from './cost-estimate';
import { ReviewedChanges, SaveReviewedFiles } from './reviewed-files';

interface GenerateReviewProps {
  flow: GenerateFlow;
  plan: IacGeneratePlan;
  /** A conexão vinculada da mesma nuvem do desenho. */
  connection: CloudConnection | null;
  linked: CloudConnection[] | null;
}

/** O diff dos arquivos, o que é cobrado e a gravação; com verificação falhando, só o que falhou e o caminho de volta. */
export function GenerateReview({ flow, plan, connection, linked }: GenerateReviewProps) {
  const { state, options, busy } = flow;
  const failed = plan.checks.filter((check) => !check.passed);
  return (
    <section className={styles.consent} aria-label="Revisar a infraestrutura gerada">
      {failed.length > 0 ? (
        <>
          <p className={styles.rowTitle}>O desenho não passou nas verificações</p>
          <ul className={styles.impacts}>
            {failed.map((check) => (
              <li key={check.id}>
                {check.description}
                {check.detail ? ` (${check.detail})` : ''}
              </li>
            ))}
          </ul>
        </>
      ) : (
        <>
          <p className={styles.rowTitle}>
            Revisar {plan.changes.length} arquivo(s) em {plan.root}
          </p>
          <ReviewedChanges changes={plan.changes} />
          <p className={styles.rowTitle}>O que é cobrado (o preço depende da região e do uso)</p>
          <ul className={styles.impacts} aria-label="O que é cobrado">
            {plan.costs.map((cost) => (
              <li key={cost.resource}>
                {cost.resource}: {cost.billing}
              </li>
            ))}
          </ul>
          {!options.costEstimate ? null : state.estimate ? (
            <CostEstimateView estimate={state.estimate} />
          ) : connection ? (
            <div className={styles.actions}>
              <Button type="button" variant="outlined" disabled={busy} onClick={() => void flow.estimate(connection)}>
                Estimar o custo com os preços da {options.label}
              </Button>
            </div>
          ) : linked === null ? (
            <p className={styles.hint}>{LINKED_UNREADABLE}</p>
          ) : (
            <p className={styles.hint}>
              Para estimar o custo, vincule uma conexão {options.label} ao projeto em{' '}
              <Link to={pathTo('cloud')}>Conexões cloud</Link> e libere nela a leitura de preços públicos.
            </p>
          )}
          {plan.notes.length > 0 ? (
            <ul className={styles.impacts} aria-label="Observações">
              {plan.notes.map((note) => (
                <li key={note}>{note}</li>
              ))}
            </ul>
          ) : null}
        </>
      )}
      {flow.error ? (
        <p className={styles.error} role="alert">
          {flow.error}
        </p>
      ) : null}
      <SaveReviewedFiles
        what="os arquivos de infraestrutura"
        busy={busy}
        canSave={failed.length === 0}
        onSave={() => void flow.save(plan.planId)}
        onBack={() => flow.dispatch({ type: 'back' })}
        backLabel="Voltar ao desenho"
      />
    </section>
  );
}
