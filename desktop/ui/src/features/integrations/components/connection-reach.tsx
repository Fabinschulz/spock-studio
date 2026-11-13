import { Disclosure } from '@/shared/components';
import type { Connection, ConnectionReach, Connector } from '@/shared/schema';

import { CONNECTION_STATUS, impactText } from '../domain/labels';
import styles from '../integrations-page.module.scss';

/** Quando o projeto não está autorizado neste computador, o motor não conhece o nome: a tela diz isso, em vez de inventar um. */
const UNKNOWN_PROJECT = 'Projeto que não está aberto neste computador';

/**
 * As quatro perguntas de uma conexão, em linguagem simples: com qual conta entra, em quais projetos é usada, o que se lê de lá
 * e se os agentes de IA podem usar esses dados como contexto. Só leitura: o que muda, muda no vínculo do projeto.
 */
export function ConnectionReachSummary({
  connection,
  connector,
  reach
}: {
  connection: Connection;
  connector: Connector | undefined;
  /** Nulo quando o motor não respondeu: a tela diz que não conseguiu, em vez de mostrar um resumo vazio que pareceria verdade. */
  reach: ConnectionReach | null;
}) {
  if (reach === null)
    return <p className={styles.hint}>O resumo do que esta conexão alcança não pôde ser lido agora.</p>;

  const name = connector?.name ?? connection.provider;
  return (
    <Disclosure summary="O que esta conexão alcança">
      <dl className={styles.reach}>
        <dt>Com qual conta o Spock Studio entra, e ela está funcionando?</dt>
        <dd>
          {connection.account} · {CONNECTION_STATUS[connection.status].label}
        </dd>

        <dt>Em quais projetos esta conexão é usada, e com qual recurso?</dt>
        <dd>
          {reach.projects.length === 0 ? (
            'Em nenhum projeto ainda.'
          ) : (
            <ul className={styles.plainList}>
              {reach.projects.map((project) => (
                <li key={`${project.projectId}:${project.resource}`}>
                  {project.projectName ?? UNKNOWN_PROJECT}: <span className={styles.mono}>{project.resourceName}</span>
                </li>
              ))}
            </ul>
          )}
        </dd>

        <dt>O que o Spock Studio lê de lá, e o que pode fazer?</dt>
        <dd>
          {reach.projects.length > 0 ? (
            <ul className={styles.plainList}>
              {reach.projects.map((project) => (
                <li key={`${project.projectId}:${project.resource}`}>
                  {project.resourceName}: {project.dataKinds.map((kind) => kind.label).join(', ')}
                </li>
              ))}
            </ul>
          ) : null}
          <p>{impactText(reach.impact, name)}</p>
        </dd>

        <dt>Os agentes de IA podem usar esses dados como contexto?</dt>
        <dd>
          {reach.projects.length === 0 ? (
            'Não há dado desta conexão em nenhum projeto.'
          ) : (
            <ul className={styles.plainList}>
              {reach.projects.map((project) => (
                <li key={`${project.projectId}:${project.resource}`}>
                  {project.resourceName}: {project.feedsKnowledge ? 'Sim.' : 'Não.'}{' '}
                  {project.feedsKnowledge ? CONTEXT_YES : CONTEXT_NO}
                </li>
              ))}
            </ul>
          )}
          <p className={styles.hint}>
            Isso vale hoje para o assistente gerencial. O índice de conhecimento dos agentes de engenharia ainda não usa
            dado sincronizado. A decisão é de cada projeto, no vínculo dele, e começa em &quot;Não&quot;.
          </p>
        </dd>
      </dl>
    </Disclosure>
  );
}

const CONTEXT_YES =
  'Títulos de itens citados em riscos entram no que o assistente envia ao provedor de IA, mas só quando todas as fontes do projeto autorizam.';

const CONTEXT_NO =
  'Ao provedor de IA vão só números, datas e identificadores de itens, nunca os títulos. Nenhum código ou documento do projeto é enviado.';
