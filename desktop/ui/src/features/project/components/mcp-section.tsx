import type { EngineClient } from '@/shared/api';
import { PageSection, ResourceView, StatusChip } from '@/shared/components';
import { useAsyncResource } from '@/shared/hooks';
import type { McpInventory, McpServer } from '@/shared/schema';
import { useCallback } from 'react';

import styles from '../project-page.module.scss';
import { readMcp } from '../service/project-api';

/** Um tipo novo aparece como veio. */
const TRANSPORT: Record<string, { label: string; runs: string }> = {
  stdio: { label: 'Programa local', runs: 'Roda na sua máquina o programa' },
  http: { label: 'Servidor remoto', runs: 'Conecta no servidor' },
  sse: { label: 'Servidor remoto (SSE)', runs: 'Conecta no servidor' },
  unknown: { label: 'Tipo não identificado', runs: 'Declarado como' }
};

/**
 * Só leitura: o Spock Studio não grava esse arquivo, e o motor nunca devolve valores de variáveis, argumentos ou
 * cabeçalhos, só onde há uma credencial escrita no arquivo.
 */
export function McpSection({ client, projectId }: { client: EngineClient; projectId: string }) {
  const load = useCallback(() => readMcp(client, projectId), [client, projectId]);
  const inventory = useAsyncResource(load);

  return (
    <PageSection title="Servidores MCP do projeto">
      <p className={styles.meta}>
        Lidos do <span className={styles.mono}>.mcp.json</span> do projeto. O Spock Studio só lê esse arquivo: não o
        grava e nunca mostra valores de variáveis, argumentos ou cabeçalhos. Servidores da sua conta pessoal não
        aparecem aqui.
      </p>
      <ResourceView
        resource={inventory}
        loading="Lendo os servidores MCP…"
        fallbackError="Não foi possível ler os servidores MCP."
      >
        {(value) => <McpBody inventory={value} />}
      </ResourceView>
    </PageSection>
  );
}

function McpBody({ inventory }: { inventory: McpInventory }) {
  if (!inventory.present)
    return <p className={styles.meta}>Este projeto não tem .mcp.json: nenhum servidor MCP declarado.</p>;
  if (!inventory.readable)
    return (
      <p className={styles.warning} role="alert">
        O .mcp.json existe, mas não é um JSON válido, então os servidores não puderam ser listados. Corrija o arquivo e
        leia de novo.
      </p>
    );
  if (inventory.servers.length === 0) return <p className={styles.meta}>O .mcp.json não declara nenhum servidor.</p>;

  const exposed = inventory.servers.filter((server) => server.literalSecrets.length > 0).length;
  const total = inventory.servers.length;
  return (
    <>
      <p className={exposed > 0 ? styles.warning : styles.meta}>
        {total} {total === 1 ? 'servidor' : 'servidores'}
        {exposed > 0 ? `, ${exposed} com credencial escrita no arquivo.` : ': nenhuma credencial escrita no arquivo.'}
      </p>
      <ul className={styles.rows} aria-label="Servidores MCP">
        {inventory.servers.map((server) => (
          <ServerCard key={server.name} server={server} />
        ))}
      </ul>
    </>
  );
}

function ServerCard({ server }: { server: McpServer }) {
  const transport = TRANSPORT[server.transport] ?? { label: server.transport, runs: 'Declarado como' };
  return (
    <li className={styles.catalogCard}>
      <div className={styles.catalogHead}>
        <h3 className={styles.catalogTitle}>{server.name}</h3>
        <span className={styles.catalogChips}>
          <StatusChip tone="neutral">{transport.label}</StatusChip>
          {server.literalSecrets.length > 0 ? (
            <StatusChip tone="warning">Credencial escrita no arquivo</StatusChip>
          ) : null}
        </span>
      </div>
      <p>
        {transport.runs} <span className={styles.mono}>{server.runs}</span>.
      </p>
      {server.env.length > 0 ? (
        <ul className={styles.plainList} aria-label={`Variáveis de ${server.name}`}>
          {server.env.map((variable) => (
            <li key={variable.name}>
              <span className={styles.mono}>{variable.name}</span>:{' '}
              {variable.referenced ? 'vem do ambiente' : 'valor escrito no arquivo'}
            </li>
          ))}
        </ul>
      ) : null}
      {server.literalSecrets.length > 0 ? (
        <div className={styles.warning}>
          <ul className={styles.plainList} aria-label={`Credenciais escritas em ${server.name}`}>
            {server.literalSecrets.map((secret) => (
              <li key={secret.where}>
                <span className={styles.mono}>{secret.where}</span>: {secret.why}
              </li>
            ))}
          </ul>
          <p>
            Troque o valor por uma referência ao ambiente, como <span className={styles.mono}>${'{NOME}'}</span>, e
            revogue a credencial atual: ela já esteve escrita num arquivo do projeto.
          </p>
        </div>
      ) : null}
    </li>
  );
}
