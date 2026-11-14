import { formatEngineDate } from '@/features/integrations';
import type { EngineClient } from '@/shared/api';
import { Disclosure, PageSection, ResourceView, StatusChip } from '@/shared/components';
import { useAsyncResource } from '@/shared/hooks';
import type { HarnessRevision, HarnessRevisions } from '@/shared/schema';
import { Button } from '@mariliz/ui-kit';
import { useCallback, useState } from 'react';

import styles from '../project-page.module.scss';
import { readRevisions } from '../service/project-api';

const VISIBLE = 5;

const plural = (count: number, one: string, many: string) => `${count} ${count === 1 ? one : many}`;

function changeText({ created, updated, removed }: HarnessRevision['components'][number]): string {
  return [
    created.length > 0 ? `criou ${plural(created.length, 'arquivo', 'arquivos')}` : null,
    updated.length > 0 ? `atualizou ${plural(updated.length, 'arquivo', 'arquivos')}` : null,
    removed.length > 0 ? `removeu ${plural(removed.length, 'arquivo', 'arquivos')}` : null
  ]
    .filter(Boolean)
    .join(', ');
}

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

/** Vem dos diários das execuções; reverter continua sendo pelo Configurar. */
export function RevisionsSection({ client, projectId }: { client: EngineClient; projectId: string }) {
  const load = useCallback(() => readRevisions(client, projectId), [client, projectId]);
  const revisions = useAsyncResource(load);
  const [showAll, setShowAll] = useState(false);

  return (
    <PageSection title="Revisões do Harness">
      <ResourceView resource={revisions} loading="Lendo as revisões…" fallbackError="Não foi possível ler as revisões.">
        {(data) => <RevisionList data={data} showAll={showAll} onShowAll={() => setShowAll(true)} />}
      </ResourceView>
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
