import type { Connection, IntegrationCatalog } from '@/shared/schema';
import { Button } from '@mariliz/ui-kit';
import { useState } from 'react';

import { useAction } from '../hooks/use-action';
import { EmptyState, ErrorState, PageSection, SuccessState } from '@/shared/components';
import styles from '../integrations-page.module.scss';
import type { IntegrationApi } from '../service/integration-api';
import { ConnectionForm } from './connection-form';
import { ConnectionRow } from './connection-row';

interface ConnectionsSectionProps {
  api: IntegrationApi;
  catalog: IntegrationCatalog;
  connections: Connection[];
  onChanged: () => void;
}

export function ConnectionsSection({ api, catalog, connections, onChanged }: ConnectionsSectionProps) {
  const { busy, error, notice, run } = useAction(onChanged);
  const [adding, setAdding] = useState<string | null>(null);
  const addingConnector = catalog.connectors.find((connector) => connector.provider === adding);

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
            busy={busy}
            onTest={() =>
              void run(
                () => api.test(connection.id),
                'Não foi possível testar a conexão.',
                // O motor não falha quando o provedor recusa: marca a conexão com erro. Só um "conectada" é sucesso.
                (tested) =>
                  tested.status === 'connected' ? `${connection.name}: o provedor aceitou a credencial.` : null
              )
            }
            onRotate={(secret) =>
              run(
                () => api.rotate(connection.id, secret),
                'Não foi possível atualizar a credencial.',
                () => `Credencial de ${connection.name} atualizada.`
              )
            }
            onDisconnect={() =>
              void run(
                () => api.disconnect(connection.id),
                'Não foi possível desconectar.',
                () => `${connection.name} desconectada: a credencial foi apagada deste computador.`
              )
            }
            onRemove={() =>
              void run(
                () => api.remove(connection.id),
                'Não foi possível remover a conexão.',
                () => `${connection.name} removida: a credencial foi apagada deste computador.`
              )
            }
          />
        ))}
      </ul>

      {notice ? <SuccessState>{notice}</SuccessState> : null}
      {error ? <ErrorState>{error}</ErrorState> : null}

      {addingConnector ? (
        <ConnectionForm
          key={addingConnector.provider}
          connector={addingConnector}
          busy={busy}
          onCancel={() => setAdding(null)}
          onSubmit={(input) =>
            void run(
              () => api.connect(input),
              'Não foi possível conectar.',
              (connected) => `${connected.name} conectada como ${connected.account}.`
            ).then((ok) => {
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
