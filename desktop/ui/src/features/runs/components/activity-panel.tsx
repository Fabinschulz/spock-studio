import { errorMessage, type EngineClient } from '@/shared/api';
import { EmptyState, ErrorState, LoadingState } from '@/shared/components';
import { pathTo } from '@/shared/routing';
import type { RunItem } from '@/shared/schema';
import { cn } from '@/shared/utils';
import { CloseIcon, TabBar } from '@mariliz/ui-kit';
import { useEffect, useId, useRef, useState, type ReactNode } from 'react';
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
  /** O que aconteceu no projeto aberto. Vem de fora porque a linha do tempo é de outra fatia; sem ele, a aba não existe. */
  projectFeed?: ReactNode;
  className?: string;
}

/**
 * O que espera a pessoa e o que aconteceu por último, sem sair da tela em que ela está: as execuções de todos os
 * projetos e, numa aba à parte, os acontecimentos do projeto aberto. É a versão de relance da área Atividade: as listas
 * completas, com filtros, continuam em Execuções e em Histórico.
 */
export function ActivityPanel({
  client,
  hosted,
  ready,
  focusRequest,
  onClose,
  projectFeed,
  className
}: ActivityPanelProps) {
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
          <ActivityTabs client={client} projectFeed={projectFeed} />
        )}
      </div>
      <div className={styles.footer}>
        <Link to={pathTo('runs')}>Ver todas as execuções</Link>
      </div>
    </aside>
  );
}

function ActivityTabs({ client, projectFeed }: Pick<ActivityPanelProps, 'client' | 'projectFeed'>) {
  const feed = useActivityFeed(client);
  const [opened, setOpened] = useState<RunItem | null>(null);
  const tasks = feed.status === 'ready' ? feed.value.tasks : [];

  // As duas abas de execuções leem a mesma resposta: enquanto ela não chega, ou se falha, as duas dizem o mesmo.
  const runs = (list: readonly RunItem[], label: string, empty: string) => {
    if (feed.status === 'loading') return <LoadingState>Lendo a atividade…</LoadingState>;
    if (feed.status === 'error')
      return (
        <ErrorState onRetry={feed.reload}>{errorMessage(feed.error, 'Não foi possível ler a atividade.')}</ErrorState>
      );
    return list.length === 0 ? (
      <EmptyState>{empty}</EmptyState>
    ) : (
      <ActivityList label={label} runs={list} onOpen={setOpened} />
    );
  };

  return (
    <>
      <TabBar
        aria-label="Atividade"
        variant="fullWidth"
        scrollButtons={false}
        tabs={[
          { label: tasks.length > 0 ? `Tarefas (${tasks.length})` : 'Tarefas' },
          { label: 'Feed' },
          ...(projectFeed ? [{ label: 'Projeto' }] : [])
        ]}
      >
        {runs(tasks, 'Tarefas', 'Nada espera você agora. O que pedir aprovação ou estiver em andamento aparece aqui.')}
        {runs(
          feed.status === 'ready' ? feed.value.recent : [],
          'Execuções recentes',
          'Nenhuma execução concluída ainda. Elas aparecem aqui assim que você aplicar ou executar algo.'
        )}
        {projectFeed}
      </TabBar>
      <RunDetailPanel client={client} run={opened} onClose={() => setOpened(null)} />
    </>
  );
}
