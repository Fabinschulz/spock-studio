import { errorMessage, type EngineClient } from '@/shared/api';
import { EmptyState, ErrorState, LoadingState } from '@/shared/components';
import { pathTo } from '@/shared/routing';
import type { RunItem } from '@/shared/schema';
import { cn } from '@/shared/utils';
import { CloseIcon, TabBar } from '@mariliz/ui-kit';
import { useEffect, useId, useRef, useState } from 'react';
import { Link } from 'react-router';

import { useActivityFeed } from '../hooks/use-activity-feed';
import { ActivityList } from './activity-list';
import styles from './activity.module.scss';
import { RunDetailPanel } from './run-detail-panel';

export const ACTIVITY_PANEL_ID = 'painel-de-atividade';

interface ActivityPanelProps {
  client: EngineClient;
  /** Fora do aplicativo desktop não há motor para perguntar. */
  hosted: boolean;
  /** O motor já respondeu que está pronto. */
  ready: boolean;
  /** Muda quando a pessoa pede o painel: o foco vai para ele. */
  focusRequest: number;
  onClose: () => void;
  className?: string;
}

/**
 * O que espera a pessoa e o que aconteceu por último, em todos os projetos, sem sair da tela em que ela está. É a
 * versão de relance da área Atividade: a lista completa, com filtros, continua em Execuções.
 */
export function ActivityPanel({ client, hosted, ready, focusRequest, onClose, className }: ActivityPanelProps) {
  const titleId = useId();
  const root = useRef<HTMLElement>(null);

  useEffect(() => {
    if (focusRequest > 0) root.current?.focus();
  }, [focusRequest]);

  return (
    <aside
      id={ACTIVITY_PANEL_ID}
      ref={root}
      tabIndex={-1}
      aria-labelledby={titleId}
      className={cn(styles.panel, className)}
    >
      <div className={styles.header}>
        <h2 id={titleId} className={styles.title}>
          Atividade
        </h2>
        <button type="button" className={styles.close} aria-label="Fechar o painel de atividade" onClick={onClose}>
          <CloseIcon fontSize="small" aria-hidden="true" />
        </button>
      </div>
      <div className={styles.body}>
        {!hosted ? (
          <EmptyState>Sem motor local, não há atividade nesta prévia. Ela aparece no aplicativo desktop.</EmptyState>
        ) : !ready ? (
          <LoadingState>Aguardando o motor…</LoadingState>
        ) : (
          <Feed client={client} />
        )}
      </div>
      <div className={styles.footer}>
        <Link to={pathTo('runs')}>Ver todas as execuções</Link>
      </div>
    </aside>
  );
}

function Feed({ client }: { client: EngineClient }) {
  const feed = useActivityFeed(client);
  const [opened, setOpened] = useState<RunItem | null>(null);

  if (feed.status === 'loading') return <LoadingState>Lendo a atividade…</LoadingState>;
  if (feed.status === 'error')
    return (
      <ErrorState onRetry={feed.reload}>{errorMessage(feed.error, 'Não foi possível ler a atividade.')}</ErrorState>
    );

  const { tasks, recent } = feed.value;
  return (
    <>
      <TabBar
        aria-label="Atividade"
        variant="fullWidth"
        scrollButtons={false}
        tabs={[{ label: tasks.length > 0 ? `Tarefas (${tasks.length})` : 'Tarefas' }, { label: 'Feed' }]}
      >
        {tasks.length === 0 ? (
          <EmptyState>Nada espera você agora. O que pedir aprovação ou estiver em andamento aparece aqui.</EmptyState>
        ) : (
          <ActivityList label="Tarefas" runs={tasks} onOpen={setOpened} />
        )}
        {recent.length === 0 ? (
          <EmptyState>
            Nenhuma execução concluída ainda. Elas aparecem aqui assim que você aplicar ou executar algo.
          </EmptyState>
        ) : (
          <ActivityList label="Execuções recentes" runs={recent} onOpen={setOpened} />
        )}
      </TabBar>
      <RunDetailPanel client={client} run={opened} onClose={() => setOpened(null)} />
    </>
  );
}
