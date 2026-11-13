import { formatEngineDate } from '@/features/integrations';
import type { SelectedProject } from '@/features/project-selection';
import type { EngineClient } from '@/shared/api';
import {
  EmptyState,
  ErrorState,
  LoadingState,
  NoProjectState,
  Page,
  PageSection,
  StatusChip,
  type Tone
} from '@/shared/components';
import { executionPath, pathTo } from '@/shared/routing';
import type { ProjectTimelineEvent } from '@/shared/schema';
import { Button } from '@mariliz/ui-kit';
import { Fragment, useState } from 'react';
import { Link } from 'react-router';

import { useTimeline } from './hooks/use-timeline';
import styles from './timeline-page.module.scss';

/** O nome de cada tipo no acontecimento. Um tipo que a tela não conhece aparece com o nome que veio. */
const KIND_LABEL: Record<string, string> = {
  harness: 'Harness',
  profile: 'Perfil',
  run: 'Execução',
  decision: 'Decisão',
  risk: 'Risco',
  sync: 'Sincronização'
};

/** O nível do acontecimento: a cor reforça, mas a palavra diz (nunca só a cor). `info` não leva aviso. */
const LEVEL: Record<string, { label: string; tone: Tone } | undefined> = {
  ok: { label: 'Deu certo', tone: 'success' },
  attention: { label: 'Atenção', tone: 'warning' },
  problem: { label: 'Problema', tone: 'danger' }
};

/**
 * A linha do tempo do projeto: revisões do Harness, mudanças do perfil, execuções, decisões, mudanças de risco e sincronizações,
 * do mais recente ao mais antigo. É só leitura: cada área continua guardando o que é dela, e a linha do tempo só junta.
 */
export function TimelinePage({ client, project }: { client: EngineClient; project: SelectedProject | null }) {
  if (!project) {
    return (
      <Page title="Linha do tempo">
        <NoProjectState />
      </Page>
    );
  }
  // A chave remonta a tela ao trocar de projeto: nada de um projeto aparece, nem por um instante, no outro.
  return (
    <Fragment key={project.projectId}>
      <Page
        title="Linha do tempo"
        description={`${project.displayName} · o que aconteceu no projeto, do mais recente ao mais antigo. Só leitura: cada área continua guardando o que é dela.`}
      >
        <TimelineView client={client} projectId={project.projectId} />
      </Page>
    </Fragment>
  );
}

function TimelineView({ client, projectId }: { client: EngineClient; projectId: string }) {
  const [kinds, setKinds] = useState<string[]>([]);
  const { state, loadMore, reload } = useTimeline(client, projectId, kinds);
  const toggle = (kind: string) =>
    setKinds((current) => (current.includes(kind) ? current.filter((item) => item !== kind) : [...current, kind]));

  return (
    <PageSection
      title="Acontecimentos"
      action={
        state.status !== 'loading' ? (
          <Button type="button" variant="outlined" size="small" onClick={reload}>
            Ler de novo
          </Button>
        ) : null
      }
    >
      {state.status === 'loading' ? <LoadingState>Lendo a linha do tempo…</LoadingState> : null}
      {state.status === 'error' ? <ErrorState onRetry={reload}>{state.message}</ErrorState> : null}
      {state.status === 'ready' ? (
        <>
          <div className={styles.filters} role="group" aria-label="Tipos de acontecimento">
            {state.sources.map((source) => (
              <Button
                key={source.kind}
                type="button"
                size="small"
                variant={kinds.includes(source.kind) ? 'contained' : 'outlined'}
                aria-pressed={kinds.includes(source.kind)}
                onClick={() => toggle(source.kind)}
              >
                {source.label} ({source.count})
              </Button>
            ))}
          </div>
          {state.sources.some((source) => !source.available) ? (
            <p className={styles.warning} role="status">
              Não foi possível ler:{' '}
              {state.sources
                .filter((source) => !source.available)
                .map((source) => source.label)
                .join(', ')}
              . O resto da linha do tempo está completo.
            </p>
          ) : null}

          {state.events.length === 0 ? (
            <EmptyState>
              {kinds.length > 0
                ? 'Nenhum acontecimento destes tipos.'
                : 'Ainda não aconteceu nada neste projeto que a linha do tempo conte.'}
            </EmptyState>
          ) : (
            <ol className={styles.events} aria-label="Acontecimentos do projeto">
              {state.events.map((event) => (
                <EventItem key={event.id} event={event} />
              ))}
            </ol>
          )}

          {state.moreError ? <ErrorState>{state.moreError}</ErrorState> : null}
          {state.next !== null ? (
            <div className={styles.more}>
              <Button type="button" variant="outlined" onClick={loadMore} disabled={state.loadingMore}>
                {state.loadingMore ? 'Carregando…' : 'Ver mais antigos'}
              </Button>
            </div>
          ) : null}
        </>
      ) : null}
    </PageSection>
  );
}

function EventItem({ event }: { event: ProjectTimelineEvent }) {
  const level = LEVEL[event.level];
  return (
    <li className={styles.event}>
      <div className={styles.eventHead}>
        <span className={styles.when}>{formatEngineDate(event.at)}</span>
        <StatusChip tone="neutral">{KIND_LABEL[event.kind] ?? event.kind}</StatusChip>
        {level ? <StatusChip tone={level.tone}>{level.label}</StatusChip> : null}
      </div>
      <p className={styles.title}>{event.title}</p>
      {event.detail ? <p className={styles.meta}>{event.detail}</p> : null}
      {event.refType === 'run' && event.refId ? <Link to={executionPath(event.refId)}>Ver execução</Link> : null}
      {event.refType === 'decision' ? <Link to={pathTo('management')}>Ver em Acompanhamento</Link> : null}
    </li>
  );
}
