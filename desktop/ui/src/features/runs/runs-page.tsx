import { formatEngineDate } from '@/features/integrations';
import type { SelectedProject } from '@/features/project-selection';
import type { EngineClient } from '@/shared/api';
import { EmptyState, ErrorState, LoadingState, Page, PageSection, StatusChip } from '@/shared/components';
import type { RunItem } from '@/shared/schema';
import { runEffectLabel, runKindLabel } from '@/shared/utils';
import { Button } from '@mariliz/ui-kit';
import { useId, useState, type ReactNode } from 'react';

import { RunDetailPanel } from './components/run-detail-panel';
import { displayState, KIND_OPTIONS, STATE_OPTIONS, stateTone } from './domain/run-labels';
import { useRunList } from './hooks/use-run-list';
import styles from './runs-page.module.scss';

const ALL = '';

/**
 * A central de execuções: toda execução passada ou em andamento, de qualquer jornada, com filtro por projeto, tipo e
 * estado, e o detalhe num painel ao lado. É leitura: abrir uma execução nunca dispara, reverte nem valida nada.
 */
export function RunsPage({ client, project }: { client: EngineClient; project: SelectedProject | null }) {
  const [onlyProject, setOnlyProject] = useState(false);
  const [kind, setKind] = useState(ALL);
  const [state, setState] = useState(ALL);
  const [opened, setOpened] = useState<RunItem | null>(null);

  const scoped = onlyProject && project !== null;
  const {
    state: list,
    loadMore,
    reload
  } = useRunList(client, {
    projectId: scoped ? project.projectId : null,
    kind: kind || null,
    state: state || null
  });

  return (
    <Page
      width="wide"
      title="Execuções"
      description={
        scoped
          ? `${project.displayName} · tudo o que o Spock Studio executou neste projeto.`
          : 'Tudo o que o Spock Studio executou nos projetos que você autorizou.'
      }
    >
      <PageSection title="Filtros">
        <div className={styles.filters}>
          {project ? (
            <Field label="Projeto">
              {(id) => (
                <select
                  id={id}
                  className={styles.select}
                  value={onlyProject ? 'one' : 'all'}
                  onChange={(event) => setOnlyProject(event.target.value === 'one')}
                >
                  <option value="all">Todos os projetos</option>
                  <option value="one">Só {project.displayName}</option>
                </select>
              )}
            </Field>
          ) : null}
          <Field label="Tipo">
            {(id) => (
              <select id={id} className={styles.select} value={kind} onChange={(event) => setKind(event.target.value)}>
                <option value={ALL}>Todos os tipos</option>
                {KIND_OPTIONS.map((option) => (
                  <option key={option.value} value={option.value}>
                    {option.label}
                  </option>
                ))}
              </select>
            )}
          </Field>
          <Field label="Estado">
            {(id) => (
              <select
                id={id}
                className={styles.select}
                value={state}
                onChange={(event) => setState(event.target.value)}
              >
                <option value={ALL}>Todos os estados</option>
                {STATE_OPTIONS.map((option) => (
                  <option key={option.value} value={option.value}>
                    {option.label}
                  </option>
                ))}
              </select>
            )}
          </Field>
        </div>
      </PageSection>

      <PageSection title="Histórico">
        {list.status === 'loading' ? <LoadingState>Lendo as execuções…</LoadingState> : null}
        {list.status === 'error' ? <ErrorState onRetry={reload}>{list.message}</ErrorState> : null}
        {list.status === 'ready' ? (
          <>
            {list.summary.running > 0 || list.summary.awaitingApproval > 0 ? (
              <p className={styles.meta}>
                {[
                  list.summary.running > 0 ? `${list.summary.running} em andamento` : null,
                  list.summary.awaitingApproval > 0 ? `${list.summary.awaitingApproval} aguardando aprovação` : null
                ]
                  .filter(Boolean)
                  .join(' · ')}
              </p>
            ) : null}
            {list.runs.length === 0 ? (
              <EmptyState>
                {kind || state || scoped
                  ? 'Nenhuma execução combina com esses filtros.'
                  : 'Nenhuma execução ainda. Elas aparecem aqui assim que você aplicar ou executar algo.'}
              </EmptyState>
            ) : (
              <ul className={styles.rows} aria-label="Execuções">
                {list.runs.map((run) => (
                  <li key={run.runId} className={styles.rowItem}>
                    <RunRow run={run} showProject={!scoped} onOpen={() => setOpened(run)} />
                  </li>
                ))}
              </ul>
            )}
            {list.moreError ? <ErrorState>{list.moreError}</ErrorState> : null}
            {list.nextCursor !== null ? (
              <div className={styles.more}>
                <Button variant="outlined" onClick={loadMore} disabled={list.loadingMore}>
                  {list.loadingMore ? 'Carregando…' : 'Carregar mais'}
                </Button>
              </div>
            ) : null}
          </>
        ) : null}
      </PageSection>

      <RunDetailPanel client={client} run={opened} onClose={() => setOpened(null)} />
    </Page>
  );
}

function Field({ label, children }: { label: string; children: (id: string) => ReactNode }) {
  const id = useId();
  return (
    <div className={styles.field}>
      <label htmlFor={id} className={styles.label}>
        {label}
      </label>
      {children(id)}
    </div>
  );
}

function RunRow({ run, showProject, onOpen }: { run: RunItem; showProject: boolean; onOpen: () => void }) {
  const effect = runEffectLabel(run);
  return (
    <button type="button" className={styles.row} onClick={onOpen} aria-haspopup="dialog">
      <span className={styles.rowMain}>
        <span className={styles.itemTitle}>{runKindLabel(run.kind)}</span>
        {showProject ? <span className={styles.meta}> · {run.projectName}</span> : null}
        {effect ? <span className={styles.meta}> · {effect}</span> : null}
      </span>
      <span className={styles.rowEnd}>
        <StatusChip tone={stateTone(run)}>{displayState(run)}</StatusChip>
        <span className={styles.meta}>{formatEngineDate(run.startedAt)}</span>
      </span>
    </button>
  );
}
