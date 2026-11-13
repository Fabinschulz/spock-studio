import { formatEngineDate } from '@/features/integrations';
import { EmptyState, PageSection, ResourceView, StatusChip, type Tone } from '@/shared/components';
import { useAsyncResource } from '@/shared/hooks';
import { pathTo } from '@/shared/routing';
import type { LinkedItem } from '@/shared/schema';
import { runKindLabel, runStateLabel } from '@/shared/utils';
import { useCallback } from 'react';
import { Link } from 'react-router';

import styles from '../management-page.module.scss';
import type { ManagementApi } from '../service/management-api';

const PROVIDER_LABEL: Record<string, string> = { github: 'GitHub', jira: 'Jira' };
const CATEGORY_TONE: Record<string, Tone> = { done: 'success', inProgress: 'info', todo: 'neutral' };

/**
 * Os itens de trabalho que têm execuções ligadas a eles: a ponte entre o que se entregou e o que foi pedido. As ligações
 * são feitas no detalhe de cada execução; aqui só se lê.
 */
export function LinkedRunsSection({ management, projectId }: { management: ManagementApi; projectId: string }) {
  const load = useCallback(() => management.linkedRuns(projectId), [management, projectId]);
  const linked = useAsyncResource(load);

  return (
    <PageSection title="Execuções ligadas a itens">
      <ResourceView
        resource={linked}
        loading="Lendo as execuções ligadas…"
        fallbackError="Não foi possível ler as execuções ligadas."
      >
        {({ items }) =>
          items.length === 0 ? (
            <EmptyState>
              Nenhuma execução foi ligada a um item deste projeto ainda. Ligue pelo detalhe de uma execução, em{' '}
              <Link to={pathTo('runs')}>Execuções</Link>.
            </EmptyState>
          ) : (
            <>
              <ul className={styles.rows} aria-label="Itens com execuções ligadas">
                {items.map((item) => (
                  <ItemRow key={`${item.integrationId}:${item.kind}:${item.externalId}`} item={item} />
                ))}
              </ul>
              <p className={styles.muted}>
                Para abrir uma execução, use o detalhe em <Link to={pathTo('runs')}>Execuções</Link>.
              </p>
            </>
          )
        }
      </ResourceView>
    </PageSection>
  );
}

function ItemRow({ item }: { item: LinkedItem }) {
  return (
    <li className={styles.portfolioRow}>
      <div className={styles.portfolioHead}>
        <span>
          <span className={styles.rowTitle}>#{item.externalId}</span>
          {item.available ? (
            <span> {item.title}</span>
          ) : (
            <span className={styles.muted}> fora do recorte atual da integração</span>
          )}
          <span className={styles.muted}>
            {' '}
            · {item.provider ? (PROVIDER_LABEL[item.provider] ?? item.provider) : 'Integração removida'}
          </span>
        </span>
        {item.available && item.status ? (
          <StatusChip tone={CATEGORY_TONE[item.statusCategory ?? ''] ?? 'neutral'}>{item.status}</StatusChip>
        ) : null}
      </div>
      <ul className={styles.plainList} aria-label={`Execuções ligadas ao item ${item.externalId}`}>
        {item.runs.map((run) => (
          <li key={run.runId} className={styles.muted}>
            {runKindLabel(run.kind)} · {runStateLabel(run.state)} · {formatEngineDate(run.startedAt)}
          </li>
        ))}
      </ul>
    </li>
  );
}
