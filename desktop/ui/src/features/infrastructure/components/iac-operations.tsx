import { createCloudApi } from '@/features/cloud';
import { formatEngineDate, useAction, useReloadable } from '@/features/integrations';
import type { SelectedProject } from '@/features/project-selection';
import { errorMessage, type EngineClient } from '@/shared/api';
import { ErrorState, LoadingState, Radio, StatusChip, SuccessState } from '@/shared/components';
import type { IacOperation } from '@/shared/schema';
import { Button, TextField } from '@mariliz/ui-kit';
import { useCallback, useMemo, useState } from 'react';

import styles from '../infrastructure-page.module.scss';
import { ENGINE_NAME } from '../domain/labels';
import type { IacApi } from '../service/iac-api';

interface IacOperationsProps {
  client: EngineClient;
  api: IacApi;
  project: SelectedProject;
  /** Sem ferramenta pronta para o projeto, validar e planejar ficam desligados. */
  ready: boolean;
}

const ACTION_LABEL: Record<string, string> = {
  create: 'criar',
  update: 'alterar',
  replace: 'substituir',
  delete: 'destruir',
  read: 'ler'
};

/** Validar e planejar: rodam numa cópia da configuração na pasta do Spock e nunca mudam a nuvem nem o projeto. */
export function IacOperations({ client, api, project, ready }: IacOperationsProps) {
  const cloud = useMemo(() => createCloudApi(client), [client]);
  const projectId = project.projectId;
  const load = useCallback(async () => {
    const [layout, operations, links, connections] = await Promise.all([
      api.layout(projectId),
      api.operations(projectId),
      cloud.links(projectId).catch(() => []),
      cloud.connections().catch(() => [])
    ]);
    return { layout, operations, links, connections };
  }, [api, cloud, projectId]);
  const hub = useReloadable(load);
  const action = useAction(hub.reload);
  const [folder, setFolder] = useState<string | null>(null);
  const [workspace, setWorkspace] = useState('');
  const [connectionId, setConnectionId] = useState('');
  const [shown, setShown] = useState<string | null>(null);

  if (!hub.settled) return <LoadingState>Procurando a configuração de infraestrutura do projeto…</LoadingState>;
  if (hub.value === null)
    return (
      <ErrorState onRetry={hub.reload}>
        {errorMessage(hub.error, 'Não foi possível ler a infraestrutura do projeto.')}
      </ErrorState>
    );

  const { layout, operations, links, connections } = hub.value;
  if (layout.folders.length === 0) return <p className={styles.meta}>Este projeto não tem arquivos .tf nem .tofu.</p>;

  const chosenFolder =
    folder ?? (layout.root !== null && layout.folders.includes(layout.root) ? layout.root : layout.folders[0]!);
  const linked = links
    .map((link) => connections.find((connection) => connection.id === link.connectionId))
    .filter((connection) => connection !== undefined);
  const current = operations.find((operation) => operation.id === shown) ?? operations[0] ?? null;

  const validate = () =>
    void action
      .run(() => api.validate(projectId), 'A validação não rodou.', {
        success: (operation) =>
          operation.status === 'succeeded' ? 'Configuração válida e formatada.' : 'A validação encontrou problemas.'
      })
      .then(() => setShown(null));

  const plan = () =>
    void action
      .run(
        () =>
          api.plan({
            projectId,
            folder: chosenFolder,
            ...(workspace.trim() ? { workspace: workspace.trim() } : {}),
            ...(connectionId ? { connectionId } : {})
          }),
        'O plano não rodou.',
        { success: (operation) => (operation.status === 'succeeded' ? 'Plano gerado e guardado.' : 'O plano falhou.') }
      )
      .then(() => setShown(null));

  return (
    <div className={styles.panel}>
      <p className={styles.hint}>
        Validar e planejar não mudam a nuvem nem o projeto: rodam numa cópia da configuração na pasta do Spock. Aplicar
        um plano é a próxima etapa e vai exigir a sua aprovação daquele plano.
      </p>

      <fieldset className={styles.choices}>
        <legend>Pasta do plano</legend>
        {layout.folders.map((option) => (
          <Radio
            key={option}
            name="iac-folder"
            value={option}
            checked={chosenFolder === option}
            disabled={action.busy}
            onChange={() => setFolder(option)}
            label={option || 'raiz do projeto'}
          />
        ))}
      </fieldset>
      <TextField
        label="Ambiente (workspace)"
        value={workspace}
        disabled={action.busy}
        onChange={(event) => setWorkspace(event.target.value)}
        helperText="Vazio usa o workspace default. O Spock só seleciona workspaces que já existem."
        slotProps={{ htmlInput: { spellCheck: false } }}
      />
      {linked.length > 0 ? (
        <fieldset className={styles.choices}>
          <legend>Credenciais do plano</legend>
          <Radio
            name="iac-connection"
            value=""
            checked={connectionId === ''}
            disabled={action.busy}
            onChange={() => setConnectionId('')}
            label="As da máquina, como o projeto configura"
          />
          {linked.map((connection) => (
            <Radio
              key={connection.id}
              name="iac-connection"
              value={connection.id}
              checked={connectionId === connection.id}
              disabled={action.busy}
              onChange={() => setConnectionId(connection.id)}
              label={`Conexão ${connection.name} (${connection.provider})`}
            />
          ))}
        </fieldset>
      ) : null}

      <div className={styles.actions}>
        <Button type="button" variant="outlined" disabled={!ready || action.busy} onClick={validate}>
          Validar
        </Button>
        <Button type="button" disabled={!ready || action.busy} onClick={plan}>
          Gerar plano
        </Button>
      </div>
      {!ready ? <p className={styles.hint}>Instale ou escolha a ferramenta acima para validar e planejar.</p> : null}
      {action.busy ? <LoadingState>Rodando… o init pode baixar providers na primeira vez.</LoadingState> : null}
      {action.error ? <p className={styles.error}>{action.error}</p> : null}
      {action.notice ? <SuccessState>{action.notice.text}</SuccessState> : null}

      {current ? <OperationResult operation={current} /> : null}

      {operations.length > 1 ? (
        <ul className={styles.rows} aria-label="Operações anteriores">
          {operations.slice(0, 10).map((operation) => (
            <li key={operation.id} className={styles.row}>
              <div className={styles.rowHead}>
                <Button type="button" variant="text" onClick={() => setShown(operation.id)}>
                  {operation.kind === 'plan' ? 'Plano' : 'Validação'} de {formatEngineDate(operation.finishedAt)}
                </Button>
                <StatusChip tone={operation.status === 'succeeded' ? 'success' : 'danger'}>
                  {operation.status === 'succeeded' ? 'Concluída' : 'Falhou'}
                </StatusChip>
              </div>
            </li>
          ))}
        </ul>
      ) : null}
    </div>
  );
}

function OperationResult({ operation }: { operation: IacOperation }) {
  const title = operation.kind === 'plan' ? 'Plano' : 'Validação';
  return (
    <section className={styles.consent} aria-label={`${title} ${operation.id}`}>
      <div className={styles.rowHead}>
        <span className={styles.rowTitle}>
          {title} · {ENGINE_NAME[operation.engine]} {operation.version}
        </span>
        <StatusChip tone={operation.status === 'succeeded' ? 'success' : 'danger'}>
          {operation.status === 'succeeded' ? 'Concluída' : 'Falhou'}
        </StatusChip>
      </div>
      <p className={styles.hint}>
        {formatEngineDate(operation.finishedAt)}
        {operation.folder !== null ? ` · pasta ${operation.folder || 'raiz'}` : ''}
        {operation.workspace ? ` · ambiente ${operation.workspace}` : ''}
        {operation.planId ? ` · plano ${operation.planId}` : ''}
      </p>
      {operation.error ? <p className={styles.error}>{operation.error}</p> : null}

      {operation.validation ? (
        <ul className={styles.rows} aria-label="Pastas validadas">
          {operation.validation.map((folder) => (
            <li key={folder.folder} className={styles.row}>
              <div className={styles.rowHead}>
                <code>{folder.folder || 'raiz'}</code>
                <StatusChip tone={folder.valid && folder.formatted ? 'success' : 'danger'}>
                  {!folder.valid ? 'Inválida' : folder.formatted ? 'Válida' : 'Fora do formato'}
                </StatusChip>
              </div>
              {folder.error ? <p className={styles.hint}>{folder.error}</p> : null}
              {folder.diagnostics.map((diagnostic, index) => (
                <p key={index} className={styles.hint}>
                  {diagnostic.severity}: {diagnostic.summary}
                  {diagnostic.file ? ` (${diagnostic.file}${diagnostic.line ? `:${diagnostic.line}` : ''})` : ''}
                </p>
              ))}
            </li>
          ))}
        </ul>
      ) : null}

      {operation.summary ? (
        <>
          <div className={styles.actions} aria-label="Resumo do plano">
            <StatusChip tone="success">Criar {operation.summary.create}</StatusChip>
            <StatusChip tone="info">Alterar {operation.summary.update}</StatusChip>
            <StatusChip tone={operation.summary.replace > 0 ? 'danger' : 'neutral'}>
              Substituir {operation.summary.replace}
            </StatusChip>
            <StatusChip tone={operation.summary.delete > 0 ? 'danger' : 'neutral'}>
              Destruir {operation.summary.delete}
            </StatusChip>
          </div>
          {operation.hasChanges === false ? (
            <p>Nenhuma mudança: a infraestrutura já está como a configuração diz.</p>
          ) : null}
          {operation.findings && operation.findings.length > 0 ? (
            <ul className={styles.impacts} aria-label="Achados de segurança">
              {operation.findings.map((finding) => (
                <li key={`${finding.rule}-${finding.address}`}>
                  <StatusChip tone={finding.severity === 'danger' ? 'danger' : 'warning'}>
                    {finding.severity === 'danger' ? 'Grave' : 'Atenção'}
                  </StatusChip>{' '}
                  {finding.message}
                </li>
              ))}
            </ul>
          ) : null}
          <ul className={styles.impacts} aria-label="Mudanças do plano">
            {operation.summary.changes.slice(0, 50).map((change) => (
              <li key={change.address}>
                {ACTION_LABEL[change.action] ?? change.action}: <code>{change.address}</code>
              </li>
            ))}
          </ul>
          {operation.summary.changes.length > 50 ? (
            <p className={styles.hint}>E mais {operation.summary.changes.length - 50} mudanças.</p>
          ) : null}
        </>
      ) : null}
    </section>
  );
}
