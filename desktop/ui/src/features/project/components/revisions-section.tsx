import { formatEngineDate } from '@/features/integrations';
import { errorMessage, type EngineClient } from '@/shared/api';
import { StatusChip, PageSection, LoadingState, ErrorState, Disclosure } from '@/shared/components';
import type { HarnessRevision, HarnessRevisions } from '@/shared/schema';
import { Button } from '@mariliz/ui-kit';
import { useEffect, useState } from 'react';

import styles from '../project-page.module.scss';
import { readRevisions } from '../service/project-api';

const VISIBLE = 5;

type State = { status: 'loading' } | { status: 'error'; message: string } | { status: 'ready'; data: HarnessRevisions };

const plural = (count: number, one: string, many: string) => `${count} ${count === 1 ? one : many}`;

/** O que a revisão fez num componente, em uma frase: "criou 1 arquivo, removeu 2". */
function changeText({ created, updated, removed }: HarnessRevision['components'][number]): string {
  return [
    created.length > 0 ? `criou ${plural(created.length, 'arquivo', 'arquivos')}` : null,
    updated.length > 0 ? `atualizou ${plural(updated.length, 'arquivo', 'arquivos')}` : null,
    removed.length > 0 ? `removeu ${plural(removed.length, 'arquivo', 'arquivos')}` : null
  ]
    .filter(Boolean)
    .join(', ');
}

/** Até três componentes, um por um; acima disso, os totais (os arquivos ficam no detalhe). */
function summaryOf(revision: HarnessRevision): string {
  const { components } = revision;
  if (components.length <= 3)
    return components.map((component) => `${component.label}: ${changeText(component)}`).join('; ');
  const total = {
    label: '',
    id: '',
    created: components.flatMap((component) => component.created),
    updated: components.flatMap((component) => component.updated),
    removed: components.flatMap((component) => component.removed)
  };
  return `${components.length} componentes: ${changeText(total)}`;
}

/**
 * Revisões do Harness: cada aplicação ou reversão que mudou arquivos no projeto, numerada, com o que mudou em cada
 * componente. Vem dos diários das execuções; reverter continua sendo pelo Configurar.
 */
export function RevisionsSection({ client, projectId }: { client: EngineClient; projectId: string }) {
  const [state, setState] = useState<State>({ status: 'loading' });
  const [showAll, setShowAll] = useState(false);

  useEffect(() => {
    let active = true;
    readRevisions(client, projectId).then(
      (data) => {
        if (active) setState({ status: 'ready', data });
      },
      (error: unknown) => {
        if (active) setState({ status: 'error', message: errorMessage(error, 'Não foi possível ler as revisões.') });
      }
    );
    return () => {
      active = false;
    };
  }, [client, projectId]);

  return (
    <PageSection title="Revisões do Harness">
      {state.status === 'loading' ? <LoadingState>Lendo as revisões…</LoadingState> : null}
      {state.status === 'error' ? <ErrorState>{state.message}</ErrorState> : null}
      {state.status === 'ready' ? (
        <RevisionList data={state.data} showAll={showAll} onShowAll={() => setShowAll(true)} />
      ) : null}
    </PageSection>
  );
}

function RevisionList({
  data,
  showAll,
  onShowAll
}: {
  data: HarnessRevisions;
  showAll: boolean;
  onShowAll: () => void;
}) {
  if (data.revisions.length === 0)
    return <p className={styles.meta}>Nenhuma aplicação mudou arquivos neste projeto ainda.</p>;

  const numberOf = new Map(data.revisions.map((revision) => [revision.runId, revision.number]));
  const visible = showAll ? data.revisions : data.revisions.slice(0, VISIBLE);

  return (
    <>
      {data.appliedByEngine && data.appliedByEngine !== data.currentEngine ? (
        <p className={styles.meta}>
          O Harness atual foi gravado pela versão {data.appliedByEngine} do Spock Studio; esta é a {data.currentEngine}.
          Se o catálogo mudou, o próximo plano em Configurar mostra o que atualizar.
        </p>
      ) : null}
      <ul className={styles.rows} aria-label="Revisões do Harness">
        {visible.map((revision) => (
          <li key={revision.runId} className={styles.harnessRow}>
            <div className={styles.row}>
              <span>
                <span className={styles.rowTitle}>Revisão {revision.number}</span>
                <span className={styles.meta}>
                  {' '}
                  ·{' '}
                  {revision.kind === 'revert'
                    ? `reversão${revision.revertsRunId && numberOf.has(revision.revertsRunId) ? ` da revisão ${numberOf.get(revision.revertsRunId)}` : ''}`
                    : 'aplicação'}{' '}
                  · {formatEngineDate(revision.at)}
                </span>
              </span>
              {revision.rolledBack ? (
                <StatusChip tone="danger">Falhou e foi desfeita na hora</StatusChip>
              ) : revision.revertedByRunId ? (
                <StatusChip tone="neutral">
                  Revertida
                  {numberOf.has(revision.revertedByRunId)
                    ? ` na revisão ${numberOf.get(revision.revertedByRunId)}`
                    : ''}
                </StatusChip>
              ) : null}
            </div>
            <Disclosure className={styles.fileList} summary={<>{summaryOf(revision)}</>}>
              <ul>
                {revision.components
                  .flatMap((component) => [
                    ...component.created.map((path) => ({ path, verb: 'criado' })),
                    ...component.updated.map((path) => ({ path, verb: 'atualizado' })),
                    ...component.removed.map((path) => ({ path, verb: 'removido' }))
                  ])
                  .map(({ path, verb }) => (
                    <li key={`${verb}:${path}`}>
                      <span className={styles.mono}>{path}</span> <span className={styles.meta}>({verb})</span>
                    </li>
                  ))}
              </ul>
            </Disclosure>
          </li>
        ))}
      </ul>
      {!showAll && data.revisions.length > VISIBLE ? (
        <Button type="button" variant="text" size="small" onClick={onShowAll}>
          Mostrar todas as {data.revisions.length} revisões
        </Button>
      ) : null}
    </>
  );
}
