import { formatEngineDate } from '@/shared/utils';
import { useAction } from '@/shared/hooks';
import { StatusChip } from '@/shared/components';
import type { CloudConnection, IacOperation, IacPlanCost } from '@/shared/schema';
import { Button } from '@mariliz/ui-kit';
import { useState } from 'react';

import styles from '../infrastructure-page.module.scss';
import type { IacApi } from '../service/iac-api';

const NOTHING = () => undefined;

const ACTION: Record<string, string> = { create: 'cria', update: 'altera', replace: 'substitui', delete: 'destrói' };

const money = (currency: string, value: number) =>
  new Intl.NumberFormat('pt-BR', { style: 'currency', currency, maximumFractionDigits: 2 }).format(value);

interface PlanCostProps {
  api: IacApi;
  projectId: string;
  plan: IacOperation;
  /** Conexão AWS vinculada ao projeto; sem ela, a tela diz como liberar os preços. */
  connection: CloudConnection | null;
}

/** O custo fixo mensal do que o plano muda, com os preços públicos da AWS; sempre uma estimativa. */
export function PlanCost({ api, projectId, plan, connection }: PlanCostProps) {
  const action = useAction(NOTHING);
  const [estimate, setEstimate] = useState<IacPlanCost | null>(null);

  if (!connection)
    return (
      <p className={styles.hint}>
        Para estimar o custo do plano, vincule uma conexão AWS ao projeto e libere nela a leitura de preços públicos.
      </p>
    );
  if (!estimate)
    return (
      <div className={styles.actions}>
        <Button
          type="button"
          variant="outlined"
          disabled={action.busy}
          onClick={() =>
            void action.run(
              async () =>
                setEstimate(await api.costPlan({ projectId, operationId: plan.id, connectionId: connection.id })),
              'Não foi possível consultar os preços pela conexão.'
            )
          }
        >
          Estimar o custo do plano
        </Button>
        {action.error ? (
          <p className={styles.error} role="alert">
            {action.error}
          </p>
        ) : null}
      </div>
    );

  const sign = estimate.monthlyDelta > 0 ? '+' : '';
  return (
    <section className={styles.consent} aria-label="Custo do plano">
      <div className={styles.rowHead}>
        <span className={styles.rowTitle}>
          {money(estimate.currency, estimate.monthlyBefore)} → {money(estimate.currency, estimate.monthlyAfter)} por mês
          ({sign}
          {money(estimate.currency, estimate.monthlyDelta)})
        </span>
        <StatusChip tone={estimate.complete ? 'info' : 'warning'}>
          {estimate.complete ? 'Estimativa' : 'Estimativa incompleta'}
        </StatusChip>
      </div>
      <p className={styles.hint}>
        {estimate.source}, {estimate.region}, consultada em {formatEngineDate(estimate.pricedAt)}.
      </p>
      <ul className={styles.impacts} aria-label="Custo por recurso">
        {estimate.lines.map((line) => (
          <li key={line.address}>
            <code>{line.address}</code> ({line.detail}): {ACTION[line.action] ?? line.action},{' '}
            {line.before === null ? '-' : money(estimate.currency, line.before)} →{' '}
            {line.after === null ? '-' : money(estimate.currency, line.after)}
            {line.problem ? ` (sem valor: ${line.problem})` : ''}
          </li>
        ))}
      </ul>
      {estimate.uncovered.length > 0 ? (
        <p className={styles.hint}>
          Fora da estimativa (sem custo fixo ou cobrados por uso): {estimate.uncovered.join(', ')}.
        </p>
      ) : null}
      <ul className={styles.impacts} aria-label="Premissas do custo do plano">
        {estimate.assumptions.map((assumption) => (
          <li key={assumption}>{assumption}</li>
        ))}
      </ul>
    </section>
  );
}
