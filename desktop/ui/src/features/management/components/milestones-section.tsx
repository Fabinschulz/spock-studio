import { Disclosure, EmptyState, PageSection, ResourceView, StatusChip } from '@/shared/components';
import { useAsyncResource } from '@/shared/hooks';
import type { MilestoneProgress, ProjectMilestones } from '@/shared/schema';
import { plural } from '@/shared/utils';
import { useCallback } from 'react';

import { milestoneOutlookLabel } from '../domain/delivery-labels';
import { formatDay } from '../domain/format';
import styles from '../management-page.module.scss';
import type { ManagementApi } from '../service/management-api';

const KIND_LABEL: Record<string, string> = { issues: 'Issues', pullRequests: 'Pull requests' };

/** A previsão usa o ritmo do tipo de dado inteiro; as premissas ficam à vista. */
export function MilestonesSection({ management, projectId }: { management: ManagementApi; projectId: string }) {
  const load = useCallback(() => management.milestones(projectId), [management, projectId]);
  const milestones = useAsyncResource(load);

  return (
    <PageSection title="Marcos">
      <ResourceView resource={milestones} loading="Lendo os marcos…" fallbackError="Não foi possível ler os marcos.">
        {(value) => <MilestonesBody value={value} />}
      </ResourceView>
    </PageSection>
  );
}

export function MilestonesBody({ value }: { value: ProjectMilestones }) {
  const unassigned = value.unassigned.filter((entry) => entry.items > 0);

  if (value.milestones.length === 0)
    return (
      <EmptyState>
        Nenhum item sincronizado pertence a um marco. No GitHub, é o milestone da issue; no Jira, a versão de correção.
        {unassigned.length > 0 ? ' Se já usa marcos, sincronize de novo para trazê-los.' : ''}
      </EmptyState>
    );

  return (
    <>
      <ul className={styles.rows} aria-label="Marcos do projeto">
        {value.milestones.map((milestone) => (
          <MilestoneRow key={`${milestone.kind}:${milestone.name}`} milestone={milestone} />
        ))}
      </ul>
      {value.omitted > 0 ? (
        <p className={styles.muted}>
          Há mais {plural(value.omitted, 'marco', 'marcos')} que não aparecem aqui: a lista mostra os{' '}
          {value.milestones.length} primeiros.
        </p>
      ) : null}
      {unassigned.length > 0 ? (
        <p className={styles.muted}>
          Sem marco:{' '}
          {unassigned
            .map((entry) => `${KIND_LABEL[entry.kind] ?? entry.kind}: ${entry.items} (${entry.open} abertos)`)
            .join('; ')}
          .
        </p>
      ) : null}
      <Disclosure className={styles.method} summary="Como a previsão do marco é calculada">
        <ul>
          {[...value.assumptions, ...value.limitations].map((line) => (
            <li key={line}>{line}</li>
          ))}
        </ul>
      </Disclosure>
    </>
  );
}

function MilestoneRow({ milestone }: { milestone: MilestoneProgress }) {
  const outlook = milestoneOutlookLabel(milestone.outlook);

  return (
    <li className={styles.portfolioRow}>
      <div className={styles.portfolioHead}>
        <span>
          <span className={styles.rowTitle}>{milestone.name}</span>
          <span className={styles.muted}>
            {' '}
            · {KIND_LABEL[milestone.kind] ?? milestone.kind}
            {milestone.dueDate ? ` · prazo ${formatDay(milestone.dueDate)}` : ' · sem prazo'}
          </span>
        </span>
        <StatusChip tone={outlook.tone}>{outlook.label}</StatusChip>
      </div>
      <p className={styles.muted}>
        {milestone.delivered} de {plural(milestone.counted, 'item', 'itens')} entregues,{' '}
        {plural(milestone.open, 'aberto', 'abertos')}
        {milestone.closedWithoutDelivery > 0
          ? `; ${plural(milestone.closedWithoutDelivery, 'concluído sem entrega', 'concluídos sem entrega')}`
          : ''}
        .
      </p>
      <p>{milestone.outlookReason}</p>
      {milestone.forecast.scenarios.length > 0 ? (
        <p className={styles.muted}>
          Previsão do que falta:{' '}
          {milestone.forecast.scenarios.map((scenario) => `${scenario.label} ${formatDay(scenario.by)}`).join(' · ')}.
        </p>
      ) : null}
    </li>
  );
}
