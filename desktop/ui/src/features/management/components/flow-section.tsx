import { InlineNotice, PageSection } from '@/shared/components';
import type { Connector, KindFlow, ProjectMetrics } from '@/shared/schema';
import { useId } from 'react';

import { formatDays, formatDecimal, formatWindow } from '../domain/format';
import styles from '../management-page.module.scss';

interface FlowSectionProps {
  metrics: ProjectMetrics;
  connectors: Connector[];
}

/** Cada número diz de onde vem; o que não tem base aparece com o motivo. */
export function FlowSection({ metrics, connectors }: FlowSectionProps) {
  if (metrics.kinds.length === 0) return null;
  const label = (kind: string) =>
    connectors.flatMap((connector) => connector.dataKinds).find((entry) => entry.id === kind)?.label ?? kind;

  return (
    <PageSection title={`Fluxo nas últimas ${metrics.periodWeeks} semanas`}>
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
        merge não contam. Lead time são os dias corridos da criação à conclusão. Cycle time são os dias corridos do
        início do trabalho à conclusão: o início é a primeira vez que o item entrou em andamento no histórico de status,
        que só o Jira informa. Tipos diferentes não são somados.
      </p>
    </PageSection>
  );
}

function cycleTimeNote(cycle: KindFlow['cycleTime']): string | undefined {
  const parts = [
    cycle.p85Days === null ? null : `85% em até ${formatDays(cycle.p85Days)}`,
    cycle.excluded > 0 && cycle.p85Days !== null
      ? `${cycle.excluded} ${cycle.excluded === 1 ? 'item sem início conhecido' : 'itens sem início conhecido'}`
      : null
  ].filter((part): part is string => part !== null);
  return parts.length > 0 ? parts.join(' · ') : undefined;
}

/** O número só vale para o que a fonte permitiu ler. */
function blockedNote(blocked: KindFlow['blocked']): string | undefined {
  if (blocked.value === null) return undefined;
  const how = [
    blocked.viaLink > 0 ? `${blocked.viaLink} por vínculo` : null,
    blocked.viaLabel > 0 ? `${blocked.viaLabel} por rótulo` : null
  ].filter((part): part is string => part !== null);
  return [how.join(', '), blocked.basis].filter((part) => part.length > 0).join('. ');
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
        <Figure
          label="Cycle time mediano"
          value={flow.cycleTime.medianDays === null ? null : formatDays(flow.cycleTime.medianDays)}
          note={cycleTimeNote(flow.cycleTime)}
          unavailable={flow.cycleTime.unavailable}
        />
        <Figure
          label="Bloqueados"
          value={flow.blocked.value}
          note={blockedNote(flow.blocked)}
          unavailable={flow.blocked.unavailable}
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
  note?: string;
  unavailable: string | null;
}

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
