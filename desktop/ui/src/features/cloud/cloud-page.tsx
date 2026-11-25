import { useAction, useReloadable } from '@/shared/hooks';
import type { SelectedProject } from '@/features/project-selection';
import { errorMessage, type EngineClient } from '@/shared/api';
import { ErrorState, LoadingState, Page, PageSection, SuccessState } from '@/shared/components';
import type { CloudProvider, CloudRead } from '@/shared/schema';
import { Button } from '@mariliz/ui-kit';
import { useCallback, useMemo, useState } from 'react';

import styles from './cloud-page.module.scss';
import { CloudConnectionForm } from './components/cloud-connection-form';
import { CloudConnectionRow } from './components/cloud-connection-row';
import { CloudProjectLinks } from './components/cloud-project-links';
import { CLOUD_STATUS } from './domain/labels';
import { createCloudApi } from './service/cloud-api';

export function CloudPage({ client, project }: { client: EngineClient; project: SelectedProject | null }) {
  const api = useMemo(() => createCloudApi(client), [client]);
  const load = useCallback(async () => {
    const [providers, connections] = await Promise.all([api.catalog(), api.connections()]);
    return { providers, connections };
  }, [api]);
  const hub = useReloadable(load);
  const action = useAction(hub.reload);
  const [adding, setAdding] = useState<CloudProvider | null>(null);

  const read = useCallback(
    async (connectionId: string, capability: string): Promise<CloudRead | null> => {
      let result: CloudRead | null = null;
      await action.run(async () => {
        result = await api.read({ connectionId, capability, limit: 50 });
      }, 'Não foi possível ler este recurso.');
      return result;
    },
    [action, api]
  );

  return (
    <Page
      title="Nuvem"
      description="Conecte AWS, Microsoft Azure e Google Cloud pelos mecanismos oficiais de cada provedor e decida, projeto a projeto, o que pode ser lido e se agentes de IA podem pedir essas leituras. Só leitura: nada é criado, alterado ou apagado nas contas."
    >
      {!hub.settled ? <LoadingState>Lendo as conexões cloud…</LoadingState> : null}
      {hub.settled && hub.value === null ? (
        <ErrorState onRetry={hub.reload}>
          {errorMessage(hub.error, 'Não foi possível ler as conexões cloud.')}
        </ErrorState>
      ) : null}

      {hub.value ? (
        <>
          <PageSection title="Provedores disponíveis">
            <ul className={styles.rows}>
              {hub.value.providers.map((provider) => (
                <li key={provider.provider} className={styles.row}>
                  <div className={styles.rowHead}>
                    <span className={styles.rowTitle}>{provider.name}</span>
                    <span className={styles.meta}>
                      {provider.mechanisms.length} {provider.mechanisms.length === 1 ? 'mecanismo' : 'mecanismos'} ·{' '}
                      {provider.capabilities.length} leituras
                    </span>
                  </div>
                  <p className={styles.meta}>{provider.mechanisms.map((mechanism) => mechanism.label).join(' · ')}</p>
                  {adding?.provider === provider.provider ? (
                    <CloudConnectionForm
                      provider={provider}
                      busy={action.busy}
                      onCancel={() => setAdding(null)}
                      onSubmit={(input) =>
                        void action
                          .run(() => api.create(input), 'Não foi possível criar a conexão.', {
                            success: (connection) => `Conexão gravada: ${CLOUD_STATUS[connection.status].label}.`
                          })
                          .then((ok) => ok && setAdding(null))
                      }
                    />
                  ) : (
                    <div className={styles.actions}>
                      <Button
                        type="button"
                        variant="outlined"
                        disabled={action.busy}
                        onClick={() => setAdding(provider)}
                      >
                        Adicionar conexão {provider.name}
                      </Button>
                    </div>
                  )}
                </li>
              ))}
            </ul>
          </PageSection>

          <PageSection title="Conexões">
            {action.error ? (
              <p className={styles.error} role="alert">
                {action.error}
              </p>
            ) : null}
            {action.notice ? <SuccessState>{action.notice.text}</SuccessState> : null}
            {hub.value.connections.length === 0 ? (
              <p className={styles.meta}>Nenhuma conexão cloud configurada.</p>
            ) : (
              <ul className={styles.rows}>
                {hub.value.connections.map((connection) => (
                  <CloudConnectionRow
                    key={connection.id}
                    connection={connection}
                    provider={hub.value?.providers.find((provider) => provider.provider === connection.provider)}
                    busy={action.busy}
                    onValidate={() =>
                      void action.run(() => api.validate(connection.id), 'Não foi possível validar.', {
                        success: (updated) => `${updated.name}: ${CLOUD_STATUS[updated.status].label}.`
                      })
                    }
                    onPermissions={() =>
                      void action.run(
                        () => api.permissions(connection.id),
                        'Não foi possível verificar as permissões.',
                        {
                          success: (updated) => `Permissões de ${updated.name} verificadas.`
                        }
                      )
                    }
                    onRemove={() =>
                      void action.run(() => api.remove(connection.id), 'Não foi possível remover a conexão.')
                    }
                    onRead={(capability) => read(connection.id, capability)}
                  />
                ))}
              </ul>
            )}
          </PageSection>

          <CloudProjectLinks
            api={api}
            project={project}
            providers={hub.value.providers}
            connections={hub.value.connections}
            onChanged={hub.reload}
          />
        </>
      ) : null}
    </Page>
  );
}
