import { useAction, useReloadable } from '@/shared/hooks';
import type { SelectedProject } from '@/features/project-selection';
import { errorMessage } from '@/shared/api';
import { ConfirmAction, ErrorState, LoadingState, PageSection } from '@/shared/components';
import { pathTo } from '@/shared/routing';
import type { CloudConnection, CloudLink, CloudProvider } from '@/shared/schema';
import { Button, Checkbox } from '@mariliz/ui-kit';
import { useCallback, useState } from 'react';
import { Link } from 'react-router';

import styles from '../cloud-page.module.scss';
import { capabilityTitle } from '../domain/labels';
import type { CloudApi } from '../service/cloud-api';

interface CloudProjectLinksProps {
  api: CloudApi;
  project: SelectedProject | null;
  providers: CloudProvider[];
  connections: CloudConnection[];
  onChanged: () => void;
}

export function CloudProjectLinks({ project, ...rest }: CloudProjectLinksProps) {
  return (
    <PageSection title={project ? `Neste projeto: ${project.displayName}` : 'Neste projeto'}>
      {project ? (
        <ProjectLinks key={project.projectId} project={project} {...rest} />
      ) : (
        <p>
          Nenhum projeto selecionado. Escolha uma pasta na <Link to={pathTo('home')}>tela inicial</Link> para liberar
          leituras de cloud a ela.
        </p>
      )}
    </PageSection>
  );
}

function ProjectLinks({
  api,
  project,
  providers,
  connections,
  onChanged
}: CloudProjectLinksProps & { project: SelectedProject }) {
  const { projectId } = project;
  const load = useCallback(() => api.links(projectId), [api, projectId]);
  const list = useReloadable(load);
  const reloadList = list.reload;
  const reload = useCallback(() => {
    reloadList();
    onChanged();
  }, [reloadList, onChanged]);
  const action = useAction(reload);
  const linked = new Set(list.value?.map((link) => link.connectionId) ?? []);
  const available = connections.filter((connection) => !linked.has(connection.id));

  if (!list.settled) return <LoadingState>Lendo os vínculos…</LoadingState>;
  if (list.value === null)
    return (
      <ErrorState onRetry={list.reload}>{errorMessage(list.error, 'Não foi possível ler os vínculos.')}</ErrorState>
    );

  return (
    <>
      <p className={styles.meta}>
        Uma conexão ativa não libera nada sozinha. Aqui você escolhe o que este projeto pode ler e se agentes de IA
        (pelo servidor MCP do Spock) podem pedir essas leituras.
      </p>
      {action.error ? <p className={styles.error}>{action.error}</p> : null}
      <ul className={styles.rows}>
        {list.value.map((link) => (
          <LinkRow
            key={link.id}
            link={link}
            connection={connections.find((connection) => connection.id === link.connectionId)}
            provider={providers.find((provider) => provider.provider === link.provider)}
            busy={action.busy}
            onSave={(capabilities, agentAccess) =>
              action.run(
                () => api.configure({ projectId, linkId: link.id, capabilities, agentAccess }),
                'Não foi possível salvar o vínculo.'
              )
            }
            onUnlink={() => action.run(() => api.unlink(projectId, link.id), 'Não foi possível desvincular.')}
          />
        ))}
      </ul>
      {available.length > 0 ? (
        <div className={styles.actions}>
          {available.map((connection) => (
            <Button
              key={connection.id}
              type="button"
              variant="outlined"
              disabled={action.busy}
              onClick={() =>
                action.run(
                  () => api.link({ projectId, connectionId: connection.id, capabilities: [], agentAccess: false }),
                  'Não foi possível vincular.'
                )
              }
            >
              Vincular {connection.name}
            </Button>
          ))}
        </div>
      ) : null}
    </>
  );
}

interface LinkRowProps {
  link: CloudLink;
  connection: CloudConnection | undefined;
  provider: CloudProvider | undefined;
  busy: boolean;
  onSave: (capabilities: string[], agentAccess: boolean) => void;
  onUnlink: () => void;
}

function LinkRow({ link, connection, provider, busy, onSave, onUnlink }: LinkRowProps) {
  const [capabilities, setCapabilities] = useState(link.capabilities);
  const [agentAccess, setAgentAccess] = useState(link.agentAccess);
  const [confirming, setConfirming] = useState(false);
  const changed =
    agentAccess !== link.agentAccess ||
    capabilities.length !== link.capabilities.length ||
    capabilities.some((capability) => !link.capabilities.includes(capability));

  return (
    <li className={styles.row}>
      <div className={styles.rowHead}>
        <span className={styles.rowTitle}>{connection?.name ?? link.connectionId}</span>
        <span className={styles.meta}>{provider?.name ?? link.provider}</span>
      </div>
      <fieldset className={styles.choices}>
        <legend>Leituras que este projeto pode fazer</legend>
        {(provider?.capabilities ?? []).map((capability) => {
          const permission = connection?.permissions.find((entry) => entry.capability === capability.id);
          const denied = permission?.state === 'denied';
          return (
            <Checkbox
              key={capability.id}
              label={`${capabilityTitle(provider, capability.id)}${denied ? ' (a identidade não tem permissão)' : ''}`}
              checked={capabilities.includes(capability.id)}
              disabled={busy}
              onChange={(_, checked) =>
                setCapabilities((current) =>
                  checked ? [...current, capability.id] : current.filter((id) => id !== capability.id)
                )
              }
            />
          );
        })}
      </fieldset>
      <Checkbox
        label="Agentes de IA podem pedir estas leituras pelo MCP"
        checked={agentAccess}
        disabled={busy}
        onChange={(_, checked) => setAgentAccess(checked)}
      />
      {confirming ? (
        <ConfirmAction
          label={`Desvincular ${connection?.name ?? 'conexão'}`}
          confirmLabel="Desvincular"
          busy={busy}
          onConfirm={() => {
            setConfirming(false);
            onUnlink();
          }}
          onCancel={() => setConfirming(false)}
        >
          Este projeto deixa de poder ler desta conexão, e agentes também.
        </ConfirmAction>
      ) : (
        <div className={styles.actions}>
          <Button
            type="button"
            variant="contained"
            disabled={busy || !changed}
            onClick={() => onSave(capabilities, agentAccess)}
          >
            Salvar
          </Button>
          <Button type="button" variant="text" disabled={busy} onClick={() => setConfirming(true)}>
            Desvincular
          </Button>
        </div>
      )}
    </li>
  );
}
