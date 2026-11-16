import { formatEngineDate } from '@/features/integrations';
import { StatusChip } from '@/shared/components';
import type { IacCostEstimate, IacCostLine } from '@/shared/schema';

import styles from '../infrastructure-page.module.scss';

const money = (currency: string, value: number) =>
  new Intl.NumberFormat('pt-BR', { style: 'currency', currency, maximumFractionDigits: 2 }).format(value);

const unitPrice = (currency: string, value: number) =>
  new Intl.NumberFormat('pt-BR', { style: 'currency', currency, maximumFractionDigits: 6 }).format(value);

const quantity = (value: number) => new Intl.NumberFormat('pt-BR', { maximumFractionDigits: 2 }).format(value);

/** A estimativa mensal com os preços públicos do provedor; sempre marcada como estimativa, nunca como fatura. */
export function CostEstimateView({ estimate }: { estimate: IacCostEstimate }) {
  const price = (line: IacCostLine) =>
    line.unitPrice === null
      ? 'sem preço único na lista'
      : `${unitPrice(estimate.currency, line.unitPrice)} por ${line.unit}`;
  return (
    <section className={styles.consent} aria-label="Estimativa de custo">
      <div className={styles.rowHead}>
        <span className={styles.rowTitle}>Estimativa: {money(estimate.currency, estimate.monthlyFixed)} por mês</span>
        <StatusChip tone={estimate.complete ? 'info' : 'warning'}>
          {estimate.complete ? 'Estimativa' : 'Estimativa incompleta'}
        </StatusChip>
      </div>
      <p className={styles.hint}>
        {estimate.source}, {estimate.region}, consultada em {formatEngineDate(estimate.pricedAt)}.
      </p>
      <ul className={styles.impacts} aria-label="Custo fixo por mês">
        {estimate.fixed.map((line) => (
          <li key={line.resource}>
            <strong>{line.resource}</strong>:{' '}
            {line.monthly === null ? 'sem valor' : money(estimate.currency, line.monthly)} ({line.basis};{' '}
            {line.quantity === null ? '' : `${quantity(line.quantity)} × `}
            {price(line)})
          </li>
        ))}
      </ul>
      {estimate.usageBased.length > 0 ? (
        <>
          <p className={styles.rowTitle}>Por uso, fora do total</p>
          <ul className={styles.impacts} aria-label="Custo por uso">
            {estimate.usageBased.map((line) => (
              <li key={line.resource}>
                <strong>{line.resource}</strong>: {price(line)} ({line.basis})
              </li>
            ))}
          </ul>
        </>
      ) : null}
      <ul className={styles.impacts} aria-label="Premissas da estimativa">
        {estimate.assumptions.map((assumption) => (
          <li key={assumption}>{assumption}</li>
        ))}
      </ul>
    </section>
  );
}
