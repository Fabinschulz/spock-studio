import { useAction } from '@/shared/hooks';
import type { Connection, ConnectionReach, IntegrationCatalog } from '@/shared/schema';
import { Button } from '@mariliz/ui-kit';
import { useState } from 'react';

import { EmptyState, ErrorState, PageSection, SuccessState } from '@/shared/components';
import styles from '../integrations-page.module.scss';
import type { IntegrationApi } from '../service/integration-api';
import { ConnectionForm } from './connection-form';
import { ConnectionRow } from './connection-row';

interface ConnectionsSectionProps {
  api: IntegrationApi;
  catalog: IntegrationCatalog;
  connections: Connection[];
  /** Nulo quando o motor não conseguiu ler o resumo das conexões. */
  reach: ConnectionReach[] | null;
  onChanged: () => void;
}

export function ConnectionsSection({ api, catalog, connections, reach, onChanged }: ConnectionsSectionProps) {
  const { busy, error, notice, run } = useAction(onChanged);
  const [adding, setAdding] = useState<string | null>(null);
  const addingConnector = catalog.connectors.find((connector) => connector.provider === adding);
  // A mensagem vai para a linha da conexão que agiu; se a linha já não existe (removida) ou a ação não é de uma linha
  // (conectar), fica na seção.
  const rowOwnsNotice = notice?.target !== undefined && connections.some((entry) => entry.id === notice.target);

  return (
    <PageSection
      title="Conexões"
      description="Uma conexão é a credencial de uma conta numa ferramenta. Ela sozinha não dá acesso a dado nenhum: cada projeto escolhe o que pode ler."
    >
      {connections.length === 0 ? <EmptyState>Nenhuma conexão ainda.</EmptyState> : null}
      <ul className={styles.rows}>
        {connections.map((connection) => (
          <ConnectionRow
            key={connection.id}
            connection={connection}
            connector={catalog.connectors.find((connector) => connector.provider === connection.provider)}
            reach={reach === null ? null : (reach.find((entry) => entry.connectionId === connection.id) ?? null)}
            busy={busy}
            notice={notice?.target === connection.id ? notice.text : null}
            onTest={() =>
              void run(() => api.test(connection.id), 'Não foi possível testar a conexão.', {
                target: connection.id,
                // O motor não falha quando o provedor recusa: marca a conexão com erro. Só um "conectada" é sucesso.
                success: (tested) =>
                  tested.status === 'connected' ? `${connection.name}: o provedor aceitou a credencial.` : null
              })
            }
            onRotate={(secret) =>
              run(() => api.rotate(connection.id, secret), 'Não foi possível atualizar a credencial.', {
                target: connection.id,
                success: () => `Credencial de ${connection.name} atualizada.`
              })
            }
            onDisconnect={() =>
              void run(() => api.disconnect(connection.id), 'Não foi possível desconectar.', {
                target: connection.id,
                success: () => `${connection.name} desconectada: a credencial foi apagada deste computador.`
              })
            }
            onRemove={() =>
              void run(() => api.remove(connection.id), 'Não foi possível remover a conexão.', {
                success: () => `${connection.name} removida: a credencial foi apagada deste computador.`
              })
            }
          />
        ))}
      </ul>

      {notice && !rowOwnsNotice ? <SuccessState>{notice.text}</SuccessState> : null}
      {error ? <ErrorState>{error}</ErrorState> : null}

      {addingConnector ? (
        <ConnectionForm
          key={addingConnector.provider}
          connector={addingConnector}
          busy={busy}
          onCancel={() => setAdding(null)}
          onSubmit={(input) =>
            void run(() => api.connect(input), 'Não foi possível conectar.', {
              success: (connected) => `${connected.name} conectada como ${connected.account}.`
            }).then((ok) => {
              if (ok) setAdding(null);
            })
          }
        />
      ) : (
        <div className={styles.actions}>
          {catalog.connectors.map((connector) => (
            <Button
              key={connector.provider}
              type="button"
              variant="outlined"
              disabled={busy || !catalog.vaultAvailable}
              onClick={() => setAdding(connector.provider)}
            >
              Conectar {connector.name}
            </Button>
          ))}
        </div>
      )}
    </PageSection>
  );
}
