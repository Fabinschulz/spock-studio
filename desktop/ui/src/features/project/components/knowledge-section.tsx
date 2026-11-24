import type { EngineClient } from '@/shared/api';
import { useAsyncResource } from '@/shared/hooks';
import { pathTo } from '@/shared/routing';
import type { KnowledgeExternalSource, KnowledgeMemory, ProjectKnowledge } from '@/shared/schema';
import { Button } from '@mariliz/ui-kit';
import { useCallback } from 'react';
import { Link } from 'react-router';

import { PageSection, Disclosure, ResourceView, StatusChip } from '@/shared/components';
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
    'A inteligência local não está ligada, então o índice não foi consultado. Ela liga na próxima execução de um pedido de engenharia.',
  runtimeUnavailable: 'A inteligência local não está disponível nesta instalação; não há índice para consultar.',
  failed: 'A inteligência local não respondeu à consulta do índice. Tente ler de novo.'
};

/** Só caminhos e tamanhos: o conteúdo dos arquivos não sai do motor. */
export function KnowledgeSection({ client, projectId }: { client: EngineClient; projectId: string }) {
  const knowledge = useAsyncResource(
    useCallback(() => readKnowledge(client, projectId), [client, projectId]),
    { keepPrevious: true }
  );

  return (
    <PageSection
      title="Conhecimento do projeto"
      action={
        knowledge.status !== 'loading' ? (
          <Button type="button" variant="outlined" size="small" onClick={knowledge.reload}>
            Ler de novo
          </Button>
        ) : null
      }
    >
      <ResourceView
        resource={knowledge}
        loading="Lendo as fontes de conhecimento…"
        fallbackError="Não foi possível ler o conhecimento do projeto."
      >
        {(value) => <KnowledgeContent knowledge={value} />}
      </ResourceView>
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

      <h3 className={styles.subsection}>Regras na declaração do projeto</h3>
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

      <ExternalSources sources={knowledge.external} />

      <DecisionMemorySection memory={knowledge.memory} />

      <Disclosure
        className={styles.history}
        summary={`Arquivos que entram (${knowledge.files.length < knowledge.allowed ? `${knowledge.files.length} de ` : ''}${knowledge.allowed})`}
      >
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
      </Disclosure>
    </>
  );
}

/**
 * Nascem fechados: sincronizar não autoriza alimentar o conhecimento, e a decisão é de cada vínculo, na central de
 * integrações.
 */
function ExternalSources({ sources }: { sources: KnowledgeExternalSource[] }) {
  return (
    <>
      <h3 className={styles.subsection}>Fontes externas (itens sincronizados)</h3>
      {sources.length === 0 ? (
        <p className={styles.meta}>
          Nenhuma ferramenta vinculada a este projeto. Itens do GitHub ou do Jira só entram aqui se você vincular a
          ferramenta em <Link to={pathTo('integrations')}>Integrações</Link> e autorizar.
        </p>
      ) : (
        <>
          <ul className={styles.rows} aria-label="Fontes externas">
            {sources.map((source) => (
              <li key={source.integrationId} className={styles.row}>
                <span>
                  <span className={styles.rowTitle}>
                    {source.providerName} {source.resourceName}
                  </span>
                  <span className={styles.meta}> · {source.dataKinds.join(', ')}</span>
                  <span className={source.authorized ? styles.meta : styles.warning}>
                    {' · '}
                    {externalDetail(source)}
                  </span>
                </span>
                <StatusChip tone={source.authorized ? 'success' : 'neutral'}>
                  {source.authorized ? 'Autorizada como contexto' : 'Não autorizada'}
                </StatusChip>
              </li>
            ))}
          </ul>
          <Disclosure className={styles.history} summary="O que passa a ser enviado quando uma fonte é autorizada">
            <ul className={styles.plainList}>
              <li>
                De cada item vão a chave, o título, o tipo, a situação, os rótulos, as datas, o item pai, os marcos e os
                bloqueios.
              </li>
              <li>Não vão os responsáveis, descrições, comentários, endereços nem credenciais.</li>
              <li>
                Quando um agente recupera um trecho, ele chega ao provedor de IA marcado como fonte externa, dentro do
                bloco de conteúdo, e o motor tira do texto qualquer linha com formato de segredo antes de enviar. Um
                item com possível segredo no título nem entra no índice.
              </li>
              <li>
                O título é escrito por terceiros: o agente é instruído a tratá-lo só como fato, e isso reduz o risco,
                mas nenhuma instrução o elimina.
              </li>
              <li>Retirar a autorização apaga os itens do índice na próxima execução, que deixa de enviá-los.</li>
            </ul>
          </Disclosure>
        </>
      )}
    </>
  );
}

/** É texto que você escreveu, então não depende da política de uma integração. */
function DecisionMemorySection({ memory }: { memory: KnowledgeMemory }) {
  return (
    <>
      <h3 className={styles.subsection}>Decisões registradas (memória do projeto)</h3>
      {memory.documents === 0 ? (
        <p className={styles.meta}>
          Nenhuma decisão registrada neste projeto. As decisões que você registrar em{' '}
          <Link to={pathTo('management')}>Acompanhamento</Link> passam a servir de memória para os agentes.
        </p>
      ) : (
        <p className={styles.meta}>
          {count(memory.documents, 'decisão', 'decisões')} no índice ·{' '}
          {[
            count(memory.indexed, 'indexada', 'indexadas'),
            count(memory.outdated, 'mudou desde a indexação', 'mudaram desde a indexação'),
            count(memory.pending, 'ainda não indexada', 'ainda não indexadas')
          ].join(', ')}
          .
        </p>
      )}
      <Disclosure className={styles.history} summary="O que vai de cada decisão e o que não vai">
        <ul className={styles.plainList}>
          <li>Vão o título, a data, o que foi decidido, o motivo e quantas execuções ligam à decisão.</li>
          <li>
            Não vão os fatos do dia (podem trazer títulos de itens que a política de integração de hoje talvez não
            permita) nem credenciais: o motor já tira do texto o que parece segredo ao registrar.
          </li>
          <li>
            Quando um agente recupera uma decisão, ela chega ao provedor de IA marcada como decisão registrada, dentro
            do bloco de conteúdo, e vale como memória, nunca como ordem.
          </li>
          <li>
            Não há interruptor por decisão: o texto é seu e vive no projeto. Se uma decisão não deve servir de memória,
            o caminho é não registrá-la.
          </li>
        </ul>
      </Disclosure>
    </>
  );
}

function externalDetail(source: KnowledgeExternalSource): string {
  if (!source.authorized) {
    return `${count(source.items, 'item sincronizado', 'itens sincronizados')}, nenhum entra no índice nem vai ao provedor de IA. A decisão é do vínculo, em Integrações.`;
  }
  const parts = [
    `${source.documents} de ${count(source.items, 'item', 'itens')} entram no índice`,
    count(source.indexed, 'indexado', 'indexados'),
    count(source.outdated, 'mudou desde a indexação', 'mudaram desde a indexação'),
    count(source.pending, 'ainda não indexado', 'ainda não indexados')
  ];
  if (source.withheld > 0) {
    parts.push(
      `${count(source.withheld, 'ficou de fora', 'ficaram de fora')} (acima do limite ou com possível segredo no texto)`
    );
  }
  return `${parts.join(', ')}.`;
}

function count(value: number, one: string, many: string): string {
  return `${value} ${value === 1 ? one : many}`;
}

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
