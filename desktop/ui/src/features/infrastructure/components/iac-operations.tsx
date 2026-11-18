import { createCloudApi } from '@/features/cloud';
import { formatEngineDate, useAction, useReloadable } from '@/features/integrations';
import type { SelectedProject } from '@/features/project-selection';
import { errorMessage, type EngineClient } from '@/shared/api';
import { ConfirmAction, ErrorState, LoadingState, Radio, StatusChip, SuccessState } from '@/shared/components';
import type { IacDriftedResource, IacOperation } from '@/shared/schema';
import { Button, TextField } from '@mariliz/ui-kit';
import { Fragment, useCallback, useMemo, useState, type ReactNode } from 'react';

import styles from '../infrastructure-page.module.scss';
import { ENGINE_NAME } from '../domain/labels';
import { PlanApproval } from './plan-approval';
import { DriftSchedule } from './drift-schedule';
import { PlanCost } from './plan-cost';
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
    const [layout, operations, links, connections, schedule] = await Promise.all([
      api.layout(projectId),
      api.operations(projectId),
      cloud.links(projectId).catch(() => []),
      cloud.connections().catch(() => []),
      api.driftSchedule(projectId).catch(() => null)
    ]);
    return { layout, operations, links, connections, schedule };
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

  const { layout, operations, links, connections, schedule } = hub.value;
  if (layout.folders.length === 0) return <p className={styles.meta}>Este projeto não tem arquivos .tf nem .tofu.</p>;

  const chosenFolder =
    folder ?? (layout.root !== null && layout.folders.includes(layout.root) ? layout.root : layout.folders[0]!);
  const linked = links
    .map((link) => connections.find((connection) => connection.id === link.connectionId))
    .filter((connection) => connection !== undefined);
  const current = operations.find((operation) => operation.id === shown) ?? operations[0] ?? null;
  // O motor diz quais planos podem ser aplicados agora (o mais recente da pasta e do ambiente, no prazo, não aplicado).
  const approval =
    current?.applyRequirements && current.summary
      ? { requirements: current.applyRequirements, summary: current.summary }
      : null;

  const validate = () =>
    void action
      .run(() => api.validate(projectId), 'A validação não rodou.', {
        success: (operation) =>
          operation.status === 'succeeded' ? 'Configuração válida e formatada.' : 'A validação encontrou problemas.'
      })
      .then(() => setShown(null));

  const target = {
    projectId,
    folder: chosenFolder,
    ...(workspace.trim() ? { workspace: workspace.trim() } : {}),
    ...(connectionId ? { connectionId } : {})
  };

  const plan = () =>
    void action
      .run(() => api.plan(target), 'O plano não rodou.', {
        success: (operation) => (operation.status === 'succeeded' ? 'Plano gerado e guardado.' : 'O plano falhou.')
      })
      .then(() => setShown(null));

  const drift = () =>
    void action
      .run(() => api.drift(target), 'A verificação de drift não rodou.', {
        success: (operation) =>
          operation.status !== 'succeeded'
            ? 'A verificação de drift falhou.'
            : `${operation.drift && operation.drift.length > 0 ? `Drift em ${operation.drift.length} recurso(s)` : 'Sem drift'}` +
              (operation.unmanaged && operation.unmanaged.length > 0
                ? `; ${operation.unmanaged.length} na conta fora do state.`
                : '.')
      })
      .then(() => setShown(null));

  const apply = (
    operationId: string,
    planId: string,
    confirmations: { destroyConfirmed: boolean; risksAccepted: boolean; workspaceConfirmation?: string }
  ) =>
    void action
      .run(() => api.apply({ projectId, operationId, planId, ...confirmations }), 'Nada foi aplicado.', {
        success: (operation) =>
          operation.apply?.verified
            ? 'Plano aplicado e verificado: o plano seguinte não tem mudanças.'
            : operation.apply?.applied
              ? 'Plano aplicado, mas a verificação encontrou diferenças.'
              : 'O apply falhou; veja o que ficou pendente.'
      })
      .then(() => setShown(null));

  const unlock = (target: IacOperation) =>
    void action
      .run(
        () =>
          api.unlock({
            projectId,
            lockId: target.lockId!,
            ...(target.folder !== null ? { folder: target.folder } : {}),
            ...(target.workspace && target.workspace !== 'default' ? { workspace: target.workspace } : {})
          }),
        'A trava não foi liberada.',
        { success: () => 'Trava liberada. Gere o plano de novo.' }
      )
      .then(() => setShown(null));

  // O alerta vale para a verificação mais recente, feita pelo botão ou pelo agendamento.
  const lastDrift = operations.find((operation) => operation.kind === 'drift' && operation.status === 'succeeded');
  const driftAlert = lastDrift && (lastDrift.hasChanges || (lastDrift.unmanaged?.length ?? 0) > 0) ? lastDrift : null;

  return (
    <div className={styles.panel}>
      <p className={styles.hint}>
        Validar, planejar e verificar drift não mudam a nuvem, o state nem o projeto: rodam numa cópia da configuração
        na pasta do Spock. Só aplicar muda a nuvem, e só com a sua aprovação daquele plano.
      </p>
      {driftAlert ? (
        <p role="status" aria-label="Alerta de drift">
          <StatusChip tone="warning">Alerta de drift</StatusChip>{' '}
          {driftAlert.requestedBy === 'schedule' ? 'A verificação automática' : 'A última verificação'} de{' '}
          {formatEngineDate(driftAlert.finishedAt)} achou {driftAlert.drift?.length ?? 0} recurso(s) mudado(s) por fora
          e {driftAlert.unmanaged?.length ?? 0} na conta fora do state.{' '}
          <Button type="button" variant="text" onClick={() => setShown(driftAlert.id)}>
            Ver
          </Button>
        </p>
      ) : null}

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
        <Button type="button" variant="outlined" disabled={!ready || action.busy} onClick={drift}>
          Verificar drift
        </Button>
      </div>
      {schedule ? (
        <DriftSchedule
          api={api}
          projectId={projectId}
          view={schedule}
          target={target}
          disabled={!ready || action.busy}
          onSaved={hub.reload}
        />
      ) : null}
      {!ready ? <p className={styles.hint}>Instale ou escolha a ferramenta acima para validar e planejar.</p> : null}
      {action.busy ? <LoadingState>Rodando… o init pode baixar providers na primeira vez.</LoadingState> : null}
      {action.error ? <p className={styles.error}>{action.error}</p> : null}
      {action.notice ? <SuccessState>{action.notice.text}</SuccessState> : null}

      {current ? (
        <OperationResult
          operation={current}
          unlock={
            current.lockId ? (
              <UnlockLock lockId={current.lockId} busy={action.busy} onConfirm={() => unlock(current)} />
            ) : null
          }
          approval={
            approval ? (
              <PlanApproval
                key={current.id}
                plan={current}
                summary={approval.summary}
                requirements={approval.requirements}
                busy={action.busy}
                onApply={(confirmations) => apply(current.id, approval.requirements.planId, confirmations)}
              />
            ) : null
          }
          cost={
            current.kind === 'plan' &&
            current.status === 'succeeded' &&
            current.billables &&
            current.billables.length > 0 ? (
              <PlanCost
                key={current.id}
                api={api}
                projectId={projectId}
                plan={current}
                connection={
                  linked.find((c) => c.id === current.connectionId && c.provider === 'aws') ??
                  linked.find((c) => c.provider === 'aws') ??
                  null
                }
              />
            ) : null
          }
        />
      ) : null}

      {operations.length > 1 ? (
        <ul className={styles.rows} aria-label="Operações anteriores">
          {operations.slice(0, 10).map((operation) => (
            <li key={operation.id} className={styles.row}>
              <div className={styles.rowHead}>
                <Button type="button" variant="text" onClick={() => setShown(operation.id)}>
                  {KIND_LABEL[operation.kind]} de {formatEngineDate(operation.finishedAt)}
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

const KIND_LABEL: Record<IacOperation['kind'], string> = {
  validate: 'Validação',
  plan: 'Plano',
  apply: 'Aplicação',
  drift: 'Verificação de drift'
};

function outcome(operation: IacOperation): { label: string; tone: 'success' | 'warning' | 'danger' } {
  if (operation.kind === 'drift' && operation.status === 'succeeded')
    return operation.hasChanges
      ? { label: 'Com drift', tone: 'warning' }
      : operation.unmanaged && operation.unmanaged.length > 0
        ? { label: 'Fora do state', tone: 'warning' }
        : { label: 'Sem drift', tone: 'success' };
  if (operation.kind === 'apply') {
    if (operation.apply?.verified) return { label: 'Aplicado e verificado', tone: 'success' };
    if (operation.apply?.applied) return { label: 'Aplicado com divergência', tone: 'warning' };
    return { label: 'Falhou', tone: 'danger' };
  }
  return operation.status === 'succeeded'
    ? { label: 'Concluída', tone: 'success' }
    : { label: 'Falhou', tone: 'danger' };
}

function UnlockLock({ lockId, busy, onConfirm }: { lockId: string; busy: boolean; onConfirm: () => void }) {
  const [confirming, setConfirming] = useState(false);
  return confirming ? (
    <ConfirmAction
      label={`Liberar a trava ${lockId}`}
      confirmLabel="Liberar a trava"
      cancelLabel="Esperar"
      busy={busy}
      onConfirm={() => {
        setConfirming(false);
        onConfirm();
      }}
      onCancel={() => setConfirming(false)}
    >
      Só libere se ninguém estiver aplicando agora: liberar a trava de um apply em andamento pode corromper o state.
    </ConfirmAction>
  ) : (
    <div className={styles.actions}>
      <Button type="button" variant="outlined" disabled={busy} onClick={() => setConfirming(true)}>
        Liberar a trava do state
      </Button>
    </div>
  );
}

function OperationResult({
  operation,
  approval,
  unlock,
  cost
}: {
  operation: IacOperation;
  approval: ReactNode;
  unlock?: ReactNode;
  cost?: ReactNode;
}) {
  const title = KIND_LABEL[operation.kind];
  const result = outcome(operation);
  return (
    <section className={styles.consent} aria-label={`${title} ${operation.id}`}>
      <div className={styles.rowHead}>
        <span className={styles.rowTitle}>
          {title} · {ENGINE_NAME[operation.engine]} {operation.version}
        </span>
        <StatusChip tone={result.tone}>{result.label}</StatusChip>
      </div>
      <p className={styles.hint}>
        {formatEngineDate(operation.finishedAt)}
        {operation.folder !== null ? ` · pasta ${operation.folder || 'raiz'}` : ''}
        {operation.workspace ? ` · ambiente ${operation.workspace}` : ''}
        {operation.planId ? ` · plano ${operation.planId}` : ''}
      </p>
      {operation.error ? <p className={styles.error}>{operation.error}</p> : null}
      {unlock}
      {operation.kind === 'plan' && operation.appliedBy ? (
        <p className={styles.hint}>Este plano já foi aplicado ({operation.appliedBy}).</p>
      ) : null}
      {operation.kind === 'plan' && operation.applyBlocker ? (
        <p className={styles.hint}>O Spock não aplica este plano: {operation.applyBlocker}</p>
      ) : null}
      {operation.apply ? <ApplyResult run={operation.apply} /> : null}
      {operation.drift ? <DriftResult drift={operation.drift} /> : null}
      {operation.unmanaged ? <UnmanagedResult operation={operation} /> : null}

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
      {cost}
      {approval}
    </section>
  );
}

function DriftResult({ drift }: { drift: IacDriftedResource[] }) {
  if (drift.length === 0) return <p>Sem drift: a nuvem está como o state registra.</p>;
  return (
    <>
      <ul className={styles.impacts} aria-label="Drift encontrado">
        {drift.map((item) => (
          <li key={item.address}>
            <StatusChip tone={item.change === 'deleted' ? 'danger' : 'warning'}>
              {item.change === 'deleted' ? 'Apagado por fora' : 'Alterado por fora'}
            </StatusChip>{' '}
            <code>{item.address}</code>
            {item.attributes.length > 0 ? ` (${item.attributes.join(', ')})` : ''}
          </li>
        ))}
      </ul>
      <p className={styles.hint}>
        Para voltar ao que o código declara, gere um plano e aprove. Para manter a mudança, atualize o código para
        declará-la. O Spock não grava o drift no state.
      </p>
    </>
  );
}

function UnmanagedResult({ operation }: { operation: IacOperation }) {
  const unmanaged = operation.unmanaged ?? [];
  return (
    <>
      {unmanaged.length === 0 ? (
        <p>Na conta não há recurso fora do state para os tipos que este projeto gerencia.</p>
      ) : (
        <ul className={styles.impacts} aria-label="Na conta e fora do state">
          {unmanaged.map((item) => (
            <li key={`${item.type}-${item.id}`}>
              <StatusChip tone="warning">Fora do state</StatusChip> {item.type} <code>{item.id}</code>
              {item.name ? ` (${item.name})` : ''}
            </li>
          ))}
        </ul>
      )}
      {(operation.unmanagedNotes ?? []).map((note) => (
        <p key={note} className={styles.hint}>
          {note}
        </p>
      ))}
      {unmanaged.length > 0 ? (
        <p className={styles.hint}>
          Recurso fora do state ou passa a ser declarado no código (import), ou é de outro dono. O Spock só compara os
          tipos que o state deste projeto já gerencia.
        </p>
      ) : null}
    </>
  );
}

function ApplyResult({ run }: { run: NonNullable<IacOperation['apply']> }) {
  const outputs = Object.entries(run.outputs);
  return (
    <div className={styles.panel} aria-label="Resultado da aplicação">
      <p>
        {run.verified
          ? 'Verificado: um plano novo depois do apply não encontrou mudanças.'
          : run.applied
            ? 'O apply terminou, mas um plano novo ainda encontra diferenças.'
            : 'O apply não terminou.'}
        {run.managedResources !== null ? ` O state tem ${run.managedResources} recurso(s).` : ''}
      </p>
      {run.remaining && run.remaining.changes.length > 0 ? (
        <ul className={styles.impacts} aria-label="Pendente depois da aplicação">
          {run.remaining.changes.map((change) => (
            <li key={change.address}>
              ainda {ACTION_LABEL[change.action] ?? change.action}: <code>{change.address}</code>
            </li>
          ))}
        </ul>
      ) : null}
      {outputs.length > 0 ? (
        <dl className={styles.facts} aria-label="Outputs">
          {outputs.map(([name, value]) => (
            <Fragment key={name}>
              <dt>{name}</dt>
              <dd>{value}</dd>
            </Fragment>
          ))}
        </dl>
      ) : null}
    </div>
  );
}
