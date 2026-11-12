import { errorMessage, type EngineClient } from '@/shared/api';
import type { ProjectKnowledge } from '@/shared/schema';
import { Button } from '@mariliz/ui-kit';
import { useCallback, useEffect, useState } from 'react';

import { PageSection, LoadingState, ErrorState } from '@/shared/components';
import styles from '../project-page.module.scss';
import { readKnowledge } from '../service/project-api';

const FILE_STATE_LABEL: Record<ProjectKnowledge['files'][number]['state'], string> = {
  indexed: 'indexado',
  outdated: 'mudou desde a indexação',
  pending: 'ainda não indexado',
  unknown: 'estado do índice não lido'
};

const INDEX_STATE_TEXT: Record<Exclude<ProjectKnowledge['indexState'], 'read'>, string> = {
  runtimeStopped:
    'O runtime de IA não está ligado, então o índice não foi consultado. Ele liga na próxima execução de um pedido de engenharia.',
  runtimeUnavailable: 'O runtime de IA não está disponível nesta instalação; não há índice para consultar.',
  failed: 'O runtime de IA não respondeu à consulta do índice. Tente ler de novo.'
};

type State =
  { status: 'loading' } | { status: 'error'; message: string } | { status: 'ready'; knowledge: ProjectKnowledge };

/**
 * O conhecimento do projeto como o índice do Agentic RAG o recebe: regras, arquivos que entram, o que ficou de fora e
 * por quê, e o estado do índice. Só caminhos e tamanhos; o conteúdo dos arquivos não sai do motor.
 */
export function KnowledgeSection({ client, projectId }: { client: EngineClient; projectId: string }) {
  const [state, setState] = useState<State>({ status: 'loading' });
  const [tick, setTick] = useState(0);
  const reload = useCallback(() => setTick((current) => current + 1), []);

  useEffect(() => {
    let active = true;
    readKnowledge(client, projectId).then(
      (knowledge) => {
        if (active) setState({ status: 'ready', knowledge });
      },
      (error: unknown) => {
        if (active)
          setState({
            status: 'error',
            message: errorMessage(error, 'Não foi possível ler o conhecimento do projeto.')
          });
      }
    );
    return () => {
      active = false;
    };
  }, [client, projectId, tick]);

  return (
    <PageSection
      title="Conhecimento do projeto"
      action={
        state.status !== 'loading' ? (
          <Button type="button" variant="outlined" size="small" onClick={reload}>
            Ler de novo
          </Button>
        ) : null
      }
    >
      {state.status === 'loading' ? <LoadingState>Lendo as fontes de conhecimento…</LoadingState> : null}
      {state.status === 'error' ? <ErrorState onRetry={reload}>{state.message}</ErrorState> : null}
      {state.status === 'ready' ? <KnowledgeContent knowledge={state.knowledge} /> : null}
    </PageSection>
  );
}

function KnowledgeContent({ knowledge }: { knowledge: ProjectKnowledge }) {
  return (
    <>
      <p className={styles.meta}>
        {knowledge.allowed === 1
          ? '1 arquivo pode alimentar o contexto dos agentes.'
          : `${knowledge.allowed} arquivos podem alimentar o contexto dos agentes.`}{' '}
        {knowledge.indexState === 'read' ? indexSummary(knowledge) : INDEX_STATE_TEXT[knowledge.indexState]}
      </p>

      <h3 className={styles.subsection}>Regras em mariliz.yaml</h3>
      {knowledge.include.length === 0 && knowledge.exclude.length === 0 ? (
        <p className={styles.meta}>Nenhuma regra declarada: entra todo arquivo permitido pelas proteções abaixo.</p>
      ) : (
        <ul className={styles.plainList}>
          {knowledge.include.length > 0 ? (
            <li>
              <span className={styles.rowTitle}>Só entram:</span>{' '}
              <span className={styles.mono}>{knowledge.include.join(', ')}</span>
            </li>
          ) : null}
          {knowledge.exclude.length > 0 ? (
            <li>
              <span className={styles.rowTitle}>Nunca entram:</span>{' '}
              <span className={styles.mono}>{knowledge.exclude.join(', ')}</span>
            </li>
          ) : null}
        </ul>
      )}

      <h3 className={styles.subsection}>Fora do conhecimento</h3>
      {knowledge.excluded.length === 0 ? (
        <p className={styles.meta}>Nenhum arquivo ficou de fora.</p>
      ) : (
        <ul className={styles.rows} aria-label="Arquivos fora do conhecimento">
          {knowledge.excluded.map((group) => (
            <li key={group.reason} className={styles.row}>
              <span>
                <span className={styles.rowTitle}>{group.label}</span>
                <span className={styles.meta}>
                  {' '}
                  · {group.examples.join(', ')}
                  {group.count > group.examples.length ? ', …' : ''}
                </span>
              </span>
              <span className={styles.end}>{group.count}</span>
            </li>
          ))}
        </ul>
      )}

      <details className={styles.history}>
        <summary>
          Arquivos que entram ({knowledge.files.length < knowledge.allowed ? `${knowledge.files.length} de ` : ''}
          {knowledge.allowed})
        </summary>
        <ul>
          {knowledge.files.map((file) => (
            <li key={file.path}>
              <span className={styles.mono}>{file.path}</span>{' '}
              <span className={file.state === 'outdated' || file.state === 'pending' ? styles.warning : styles.meta}>
                ({FILE_STATE_LABEL[file.state]})
              </span>
            </li>
          ))}
        </ul>
      </details>
    </>
  );
}

function count(value: number, one: string, many: string): string {
  return `${value} ${value === 1 ? one : many}`;
}

/** "No índice: 1 indexado, 2 mudaram desde a indexação, 1 ainda não indexado." */
function indexSummary(knowledge: ProjectKnowledge): string {
  const parts = [
    count(knowledge.indexed, 'indexado', 'indexados'),
    count(knowledge.outdated, 'mudou desde a indexação', 'mudaram desde a indexação'),
    count(knowledge.pending, 'ainda não indexado', 'ainda não indexados')
  ];
  if (knowledge.orphaned > 0) {
    parts.push(
      count(knowledge.orphaned, 'no índice sem arquivo correspondente', 'no índice sem arquivo correspondente')
    );
  }
  return `No índice: ${parts.join(', ')}.`;
}
