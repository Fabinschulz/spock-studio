import { Disclosure, EmptyState, PageSection } from '@/shared/components';
import { pathTo } from '@/shared/routing';
import type { ProjectIntegration } from '@/shared/schema';
import { formatEngineDate } from '@/shared/utils';
import { Link } from 'react-router';

import { summarizeActivity } from '../domain/activity-summary';
import styles from '../management.module.scss';
import { Figure, Figures } from './figures';

/** Só conta o que foi sincronizado: fonte sem sincronização não entra como zero atividade. */
export function ActivitySection({ integrations }: { integrations: ProjectIntegration[] }) {
  const summary = summarizeActivity(integrations);

  return (
    <PageSection title="Atividades">
      {summary.total === 0 ? (
        <EmptyState>
          Não há dados de atividades: as fontes vinculadas ainda não trouxeram nenhum item. Sincronize em{' '}
          <Link to={pathTo('integrations')}>Conexões › Serviços</Link>.
        </EmptyState>
      ) : (
        <>
          <Figures>
            {summary.byCategory.map((entry) => (
              <Figure key={entry.category} label={entry.label} value={entry.count} />
            ))}
          </Figures>
          {summary.oldestReadAt ? (
            <p className={styles.muted}>Leitura mais antiga: {formatEngineDate(summary.oldestReadAt)}.</p>
          ) : null}
          <Disclosure className={styles.method} summary="Como as atividades são contadas">
            <p>
              É a contagem do que foi sincronizado. No GitHub, issue aberta conta como a fazer e pull request aberto
              como em andamento; no Jira vale a categoria do status.
            </p>
          </Disclosure>
        </>
      )}
    </PageSection>
  );
}
