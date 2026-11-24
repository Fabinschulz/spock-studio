import { formatEngineDate } from '@/shared/utils';
import { Disclosure, EmptyState, PageSection, ResourceView, StatusChip } from '@/shared/components';
import { useAsyncResource } from '@/shared/hooks';
import type { ProjectRiskHistory, RiskTrail } from '@/shared/schema';
import { useCallback } from 'react';

import { severityLabel } from '../domain/delivery-labels';
import { describeRiskChange } from '../domain/risk-history-text';
import styles from '../management-page.module.scss';
import type { ManagementApi } from '../service/management-api';

const plural = (count: number, one: string, many: string) => `${count} ${count === 1 ? one : many}`;

/** Vem dos registros que o motor faz a cada sincronização completa, então só existe a partir do primeiro registro. */
export function RiskHistorySection({ management, projectId }: { management: ManagementApi; projectId: string }) {
  const load = useCallback(() => management.riskHistory(projectId), [management, projectId]);
  const history = useAsyncResource(load);

  return (
    <PageSection title="Histórico dos riscos">
      <ResourceView
        resource={history}
        loading="Lendo o histórico dos riscos…"
        fallbackError="Não foi possível ler o histórico dos riscos."
      >
        {(value) => <HistoryBody value={value} />}
      </ResourceView>
    </PageSection>
  );
}

function HistoryBody({ value }: { value: ProjectRiskHistory }) {
  if (value.snapshots === 0)
    return (
      <EmptyState>
        Ainda não há histórico. O primeiro registro é feito quando uma sincronização terminar com todas as fontes
        completas.
      </EmptyState>
    );

  return (
    <>
      <p className={styles.muted}>
        {plural(value.snapshots, 'registro', 'registros')}, desde {formatEngineDate(value.since!)}; conferido pela
        última vez {formatEngineDate(value.lastCheckedAt!)}.
      </p>
      {value.trails.length === 0 ? (
        <EmptyState>Nenhuma regra de risco disparou nos registros feitos até agora.</EmptyState>
      ) : (
        <ul className={styles.rows} aria-label="Trajetória de cada risco">
          {value.trails.map((trail) => (
            <TrailRow key={trail.id} trail={trail} />
          ))}
        </ul>
      )}
      {value.omitted > 0 ? (
        <p className={styles.muted}>
          Há mais {plural(value.omitted, 'risco', 'riscos')} no histórico que não aparecem aqui.
        </p>
      ) : null}
      <p className={styles.muted}>{value.basis}</p>
    </>
  );
}

function TrailRow({ trail }: { trail: RiskTrail }) {
  const current = trail.currentSeverity !== null ? severityLabel(trail.currentSeverity) : null;

  return (
    <li className={styles.portfolioRow}>
      <div className={styles.portfolioHead}>
        <span className={styles.rowTitle}>{trail.title}</span>
        {trail.active && current ? (
          <StatusChip tone={current.tone}>Ativo, gravidade {current.label}</StatusChip>
        ) : (
          <StatusChip tone="success">Resolvido {trail.resolvedAt ? formatEngineDate(trail.resolvedAt) : ''}</StatusChip>
        )}
      </div>
      <p className={styles.muted}>Visto pela primeira vez {formatEngineDate(trail.firstSeenAt)}.</p>
      <Disclosure summary={`Trajetória (${plural(trail.changes.length, 'registro', 'registros')})`}>
        <ul className={styles.plainList} aria-label={`Mudanças de ${trail.title}`}>
          {trail.changes.map((change) => (
            <li key={`${change.at}:${change.kind}`}>
              <span className={styles.muted}>{formatEngineDate(change.at)}: </span>
              {describeRiskChange(change)}
            </li>
          ))}
        </ul>
      </Disclosure>
    </li>
  );
}
