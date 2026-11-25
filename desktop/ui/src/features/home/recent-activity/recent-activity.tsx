import { ActivityList, RunDetailPanel, useActivityFeed } from '@/features/runs';
import type { EngineClient } from '@/shared/api';
import { pathTo } from '@/shared/routing';
import type { RunItem } from '@/shared/schema';
import { useId, useState } from 'react';
import { Link } from 'react-router';

import styles from './recent-activity.module.scss';

const MAX_ROWS = 3;

/**
 * Um resumo discreto para continuar o trabalho: primeiro o que espera a pessoa, depois o que terminou por último. Sem
 * nada para mostrar, ou sem resposta do motor, o bloco não aparece: a lista completa e o erro ficam em Atividade.
 */
export function RecentActivity({ client }: { client: EngineClient }) {
  const titleId = useId();
  const feed = useActivityFeed(client);
  const [opened, setOpened] = useState<RunItem | null>(null);

  if (feed.status !== 'ready') return null;
  const { tasks, recent } = feed.value;
  if (tasks.length === 0 && recent.length === 0) return null;

  return (
    <section className={styles.section} aria-labelledby={titleId}>
      <div className={styles.header}>
        <h2 id={titleId} className={styles.title}>
          Atividade recente
        </h2>
        <Link to={pathTo('runs')} className={styles.all}>
          Ver toda a atividade
        </Link>
      </div>
      {tasks.length > 0 ? (
        <ActivityList label="Esperando você ou em andamento" runs={tasks.slice(0, MAX_ROWS)} onOpen={setOpened} />
      ) : null}
      {recent.length > 0 ? (
        <ActivityList label="Últimas execuções" runs={recent.slice(0, MAX_ROWS)} onOpen={setOpened} />
      ) : null}
      <RunDetailPanel client={client} run={opened} onClose={() => setOpened(null)} />
    </section>
  );
}
