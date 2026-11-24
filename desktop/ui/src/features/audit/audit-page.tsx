import { formatEngineDate } from '@/shared/utils';
import type { SelectedProject } from '@/features/project-selection';
import type { EngineClient } from '@/shared/api';
import {
  EmptyState,
  ErrorState,
  LoadingState,
  NoProjectState,
  Page,
  PageSection,
  StatusChip
} from '@/shared/components';
import { executionPath } from '@/shared/routing';
import type { ProjectAuditEntry } from '@/shared/schema';
import { Button } from '@mariliz/ui-kit';
import { Fragment, useState } from 'react';
import { Link } from 'react-router';

import styles from './audit-page.module.scss';
import { useAudit } from './hooks/use-audit';

export function AuditPage({ client, project }: { client: EngineClient; project: SelectedProject | null }) {
  if (!project) {
    return (
      <Page title="Auditoria">
        <NoProjectState />
      </Page>
    );
  }
  // A chave remonta a tela ao trocar de projeto: nada de um projeto aparece, nem por um instante, no outro.
  return (
    <Fragment key={project.projectId}>
      <Page
        title="Auditoria"
        description={`${project.displayName} · o que mudou o que o Spock Studio pode acessar ou fazer neste projeto. Só leitura: a trilha só acrescenta.`}
      >
        <AuditView client={client} projectId={project.projectId} />
      </Page>
    </Fragment>
  );
}

function AuditView({ client, projectId }: { client: EngineClient; projectId: string }) {
  const [categories, setCategories] = useState<string[]>([]);
  const { state, loadMore, reload } = useAudit(client, projectId, categories);
  const toggle = (category: string) =>
    setCategories((current) =>
      current.includes(category) ? current.filter((item) => item !== category) : [...current, category]
    );

  return (
    <PageSection
      title="Entradas"
      description="A trilha diz o que mudou e quando, não quem: o produto ainda não tem identidade de pessoa. Nunca traz credencial, texto de pedido, título de item nem o texto de uma decisão."
      action={
        state.status !== 'loading' ? (
          <Button type="button" variant="outlined" size="small" onClick={reload}>
            Ler de novo
          </Button>
        ) : null
      }
    >
      {state.status === 'loading' ? <LoadingState>Lendo a trilha de auditoria…</LoadingState> : null}
      {state.status === 'error' ? <ErrorState onRetry={reload}>{state.message}</ErrorState> : null}
      {state.status === 'ready' ? (
        <>
          <div className={styles.filters} role="group" aria-label="Categorias da auditoria">
            {state.categories
              .filter((category) => category.count > 0 || categories.includes(category.id))
              .map((category) => (
                <Button
                  key={category.id}
                  type="button"
                  size="small"
                  variant={categories.includes(category.id) ? 'contained' : 'outlined'}
                  aria-pressed={categories.includes(category.id)}
                  onClick={() => toggle(category.id)}
                >
                  {category.label} ({category.count})
                </Button>
              ))}
          </div>
          {state.sources.some((source) => !source.available) ? (
            <p className={styles.warning}>
              Não foi possível ler:{' '}
              {state.sources
                .filter((source) => !source.available)
                .map((source) => source.label)
                .join(', ')}
              . O resto da trilha está completo.
            </p>
          ) : null}

          {state.entries.length === 0 ? (
            <EmptyState>
              {categories.length > 0
                ? 'Nenhuma entrada destas categorias.'
                : 'Ainda não há nada a registrar na auditoria deste projeto.'}
            </EmptyState>
          ) : (
            <ol className={styles.entries} aria-label="Entradas da auditoria">
              {state.entries.map((entry) => (
                <EntryItem key={entry.id} entry={entry} label={labelOf(state.categories, entry.category)} />
              ))}
            </ol>
          )}

          {state.moreError ? <ErrorState>{state.moreError}</ErrorState> : null}
          {state.next !== null ? (
            <div className={styles.more}>
              <Button type="button" variant="outlined" onClick={loadMore} disabled={state.loadingMore}>
                {state.loadingMore ? 'Carregando…' : 'Ver mais antigas'}
              </Button>
            </div>
          ) : null}
        </>
      ) : null}
    </PageSection>
  );
}

function labelOf(categories: { id: string; label: string }[], id: string): string {
  return categories.find((category) => category.id === id)?.label ?? id;
}

function EntryItem({ entry, label }: { entry: ProjectAuditEntry; label: string }) {
  return (
    <li className={styles.entry}>
      <div className={styles.entryHead}>
        <span className={styles.when}>{formatEngineDate(entry.at)}</span>
        <StatusChip tone="neutral">{label}</StatusChip>
      </div>
      <p className={styles.title}>{entry.title}</p>
      {entry.detail ? <p className={styles.meta}>{entry.detail}</p> : null}
      <p className={styles.reference}>
        {entry.source === 'journal' && entry.runId ? (
          <>
            Diário da execução <span className={styles.mono}>{entry.runId}</span> ·{' '}
            <Link to={executionPath(entry.runId)}>Ver execução</Link>
          </>
        ) : null}
        {entry.source === 'audit' && entry.auditLine !== null ? (
          <>
            Arquivo de auditoria, linha <span className={styles.mono}>{entry.auditLine}</span>
          </>
        ) : null}
      </p>
    </li>
  );
}
