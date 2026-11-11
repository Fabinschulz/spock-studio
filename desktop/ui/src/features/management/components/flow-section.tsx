import { InlineNotice } from '@/shared/components';
import type { Connector, KindFlow, ProjectMetrics } from '@/shared/schema';
import { useId } from 'react';

import { formatDays, formatDecimal, formatWindow } from '../domain/format';
import styles from '../management-page.module.scss';

interface FlowSectionProps {
  metrics: ProjectMetrics;
  connectors: Connector[];
}

/** Indicadores de fluxo por tipo de dado. Cada número diz de onde vem; o que não tem base aparece com o motivo. */
export function FlowSection({ metrics, connectors }: FlowSectionProps) {
  if (metrics.kinds.length === 0) return null;
  const label = (kind: string) =>
    connectors.flatMap((connector) => connector.dataKinds).find((entry) => entry.id === kind)?.label ?? kind;

  return (
    <section aria-labelledby="fluxo">
      <h2 id="fluxo" className={styles.section}>
        Fluxo nas últimas {metrics.periodWeeks} semanas
      </h2>
      {!metrics.complete ? (
        <InlineNotice tone="warning" title="Dados incompletos">
          Ao menos uma fonte teve a última sincronização parcial ou com falha. Os números abaixo valem para o que
          chegou.
        </InlineNotice>
      ) : null}
      {metrics.kinds.map((flow) => (
        <KindFlowBlock key={flow.kind} flow={flow} label={label(flow.kind)} />
      ))}
      <p className={styles.muted}>
        Entregue é o item concluído com data de conclusão; issue descartada ou duplicada e pull request fechado sem
        merge não contam. Lead time são os dias corridos da criação à conclusão. Tipos diferentes não são somados.
      </p>
    </section>
  );
}

function KindFlowBlock({ flow, label }: { flow: KindFlow; label: string }) {
  const titleId = useId();
  const peak = Math.max(1, ...flow.throughput.weeks.map((week) => Math.max(week.delivered, week.created)));

  return (
    <div className={styles.kind} role="group" aria-labelledby={titleId}>
      <h3 id={titleId} className={styles.kindTitle}>
        {label}
      </h3>
      <dl className={styles.figures}>
        <Figure label="Em andamento" value={flow.wip.value} unavailable={flow.wip.unavailable} />
        <Figure
          label="Entregues por semana (média)"
          value={flow.throughput.weeklyAverage === null ? null : formatDecimal(flow.throughput.weeklyAverage)}
          unavailable={flow.throughput.unavailable}
        />
        <Figure
          label="Lead time mediano"
          value={flow.leadTime.medianDays === null ? null : formatDays(flow.leadTime.medianDays)}
          note={flow.leadTime.p85Days === null ? undefined : `85% em até ${formatDays(flow.leadTime.p85Days)}`}
          unavailable={flow.leadTime.unavailable}
        />
        <Figure label="Prazo vencido" value={flow.overdue.value} unavailable={flow.overdue.unavailable} />
      </dl>

      {flow.throughput.weeks.length > 0 ? (
        <table className={styles.weeks}>
          <caption className="visually-hidden">{label}: itens entregues e itens novos por semana</caption>
          <thead>
            <tr>
              <th scope="col">Semana</th>
              <th scope="col">Entregues</th>
              <th scope="col">Novos</th>
            </tr>
          </thead>
          <tbody>
            {flow.throughput.weeks.map((week) => (
              <tr key={week.from}>
                <th scope="row">{formatWindow(week.from, week.to)}</th>
                <td>
                  <Bar value={week.delivered} peak={peak} tone="delivered" />
                </td>
                <td>
                  <Bar value={week.created} peak={peak} tone="created" />
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      ) : null}
    </div>
  );
}

interface FigureProps {
  label: string;
  value: number | string | null;
  /** Complemento do valor (ex.: o percentil 85 do lead time). */
  note?: string;
  unavailable: string | null;
}

/** Sem valor, o lugar do número diz "sem dado", alinhado aos demais, e o motivo vai em nota abaixo do rótulo. */
function Figure({ label, value, note, unavailable }: FigureProps) {
  const detail = value === null ? (unavailable ?? 'Sem dado.') : note;
  return (
    <div className={styles.figure}>
      <dt>
        {label}
        {detail ? <span className={styles.figureNote}>{detail}</span> : null}
      </dt>
      {value === null ? <dd className={styles.absent}>sem dado</dd> : <dd>{value}</dd>}
    </div>
  );
}

/** A barra é só leitura rápida; o número ao lado é o dado. */
function Bar({ value, peak, tone }: { value: number; peak: number; tone: 'delivered' | 'created' }) {
  return (
    <span className={styles.barCell}>
      <span className={styles.barValue}>{value}</span>
      <span
        className={tone === 'delivered' ? styles.barDelivered : styles.barCreated}
        style={{ width: `${(value / peak) * 100}%` }}
        aria-hidden="true"
      />
    </span>
  );
}
