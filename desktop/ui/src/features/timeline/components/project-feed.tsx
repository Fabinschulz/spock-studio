import type { SelectedProject } from '@/features/project-selection';
import type { EngineClient } from '@/shared/api';
import { EmptyState, ErrorState, LoadingState } from '@/shared/components';
import { pathTo } from '@/shared/routing';
import { Link } from 'react-router';

import { useTimeline } from '../hooks/use-timeline';
import styles from '../timeline-page.module.scss';
import { TimelineEvents } from './timeline-events';

const MAX_EVENTS = 12;
const NO_FILTER: string[] = [];

interface ProjectFeedProps {
  client: EngineClient;
  project: SelectedProject | null;
}

/**
 * O que aconteceu por último no projeto aberto, para ver de relance: execuções, decisões, riscos e sincronizações. É a
 * mesma linha do tempo da tela, sem os filtros nem a paginação, que continuam lá.
 */
export function ProjectFeed({ client, project }: ProjectFeedProps) {
  if (!project) return <EmptyState>Abra um projeto para ver o que aconteceu nele.</EmptyState>;
  // A chave remonta a lista ao trocar de projeto: nada de um projeto aparece, nem por um instante, no outro.
  return <Events key={project.projectId} client={client} project={project} />;
}

function Events({ client, project }: { client: EngineClient; project: SelectedProject }) {
  const { state, reload } = useTimeline(client, project.projectId, NO_FILTER);

  if (state.status === 'loading') return <LoadingState>Lendo a linha do tempo…</LoadingState>;
  if (state.status === 'error') return <ErrorState onRetry={reload}>{state.message}</ErrorState>;
  if (state.events.length === 0)
    return <EmptyState>Ainda não aconteceu nada em {project.displayName} que a linha do tempo conte.</EmptyState>;

  const shown = state.events.slice(0, MAX_EVENTS);
  const hasMore = state.events.length > shown.length || state.next !== null;
  return (
    <>
      <TimelineEvents events={shown} label={`Acontecimentos de ${project.displayName}`} />
      {hasMore ? (
        <p className={styles.more}>
          <Link to={pathTo('timeline')}>Ver a linha do tempo completa</Link>
        </p>
      ) : null}
    </>
  );
}
