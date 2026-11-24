import { useAction, useReloadable } from '@/shared/hooks';
import type { SelectedProject } from '@/features/project-selection';
import { errorMessage } from '@/shared/api';
import {
  ConfirmAction,
  DiffView,
  ErrorState,
  LoadingState,
  Radio,
  StatusChip,
  SuccessState
} from '@/shared/components';
import { pathTo } from '@/shared/routing';
import type { CloudConnection, IacBootstrapPlan, IacStateRead, IacStateRoot } from '@/shared/schema';
import { Button, Checkbox, TextField } from '@mariliz/ui-kit';
import { useCallback, useState } from 'react';
import { Link } from 'react-router';

import styles from '../infrastructure-page.module.scss';
import { LINKED_UNREADABLE } from '../domain/linked-connections';
import type { BootstrapInput, IacApi } from '../service/iac-api';

interface StatePanelProps {
  api: IacApi;
  project: SelectedProject;
  /** Lidas uma vez pela página; `null` quando a leitura falhou. */
  linked: CloudConnection[] | null;
  /** Sem ferramenta pronta, consultar o state fica desligado; ler a configuração e o bootstrap não precisam dela. */
  ready: boolean;
}

const MODE: Record<IacStateRoot['mode'], { label: string; tone: 'success' | 'info' | 'warning' }> = {
  remote: { label: 'Remoto', tone: 'success' },
  localAbsolute: { label: 'Local, fora do projeto', tone: 'info' },
  localInProject: { label: 'Local, no projeto', tone: 'warning' }
};

const LEVEL = { info: 'info', warning: 'warning', danger: 'danger' } as const;

type Provider = BootstrapInput['provider'];

const FIELDS: Record<Provider, { name: string; label: string; hint: string }[]> = {
  aws: [
    { name: 'bucket', label: 'Bucket de state', hint: 'No modelo: <org>-terraform-state-<conta>-<região>.' },
    { name: 'region', label: 'Região (opcional com conexão)', hint: 'Ex.: sa-east-1. Sem ela, vale a da conexão.' },
    { name: 'profile', label: 'Perfil SSO (opcional)', hint: 'Sem ele, vale o da conexão ou a cadeia padrão.' },
    { name: 'account', label: 'Conta (opcional)', hint: 'Vira allowed_account_ids. Sem ela, vale a da conexão.' }
  ],
  azure: [
    { name: 'resourceGroup', label: 'Grupo de recursos', hint: 'Ex.: rg-loja-state.' },
    { name: 'storageAccount', label: 'Storage account', hint: '3 a 24 letras minúsculas e dígitos.' },
    { name: 'location', label: 'Região (opcional com conexão)', hint: 'Ex.: brazilsouth.' },
    { name: 'subscription', label: 'Subscription (opcional com conexão)', hint: 'GUID da subscription.' }
  ],
  gcp: [
    { name: 'bucket', label: 'Bucket de state', hint: 'Ex.: loja-tf-state.' },
    { name: 'gcpProject', label: 'Projeto do Google Cloud (opcional com conexão)', hint: 'Ex.: loja-dev-123.' },
    { name: 'location', label: 'Localização', hint: 'Ex.: US ou southamerica-east1.' }
  ]
};

/** Onde vive o state de cada raiz, os riscos, a consulta (só endereços) e o bootstrap do bucket no formato do modelo. */
export function StatePanel({ api, project, linked, ready }: StatePanelProps) {
  const projectId = project.projectId;
  const load = useCallback(() => api.state(projectId), [api, projectId]);
  const report = useReloadable(load);
  const action = useAction(report.reload);
  const [read, setRead] = useState<(IacStateRead & { folder: string }) | null>(null);
  const [provider, setProvider] = useState<Provider | null>(null);
  const [values, setValues] = useState<Record<string, string>>({});
  const [plan, setPlan] = useState<IacBootstrapPlan | null>(null);
  const [confirming, setConfirming] = useState(false);
  const [savedRun, setSavedRun] = useState<string | null>(null);
  const [useConnection, setUseConnection] = useState(true);

  if (!report.settled) return <LoadingState>Lendo onde vive o state…</LoadingState>;
  if (report.value === null)
    return (
      <ErrorState onRetry={report.reload}>{errorMessage(report.error, 'Não foi possível ler o state.')}</ErrorState>
    );

  const { roots, recommendation } = report.value;
  const connection = provider ? linked?.find((candidate) => candidate.provider === provider) : undefined;
  const input = (): BootstrapInput => ({
    projectId,
    provider: provider!,
    values,
    ...(connection && useConnection ? { connectionId: connection.id } : {})
  });

  return (
    <div className={styles.panel}>
      {roots.length === 0 ? (
        <p className={styles.meta}>Nenhuma raiz de infraestrutura (pasta com bloco provider ou backend).</p>
      ) : (
        <ul className={styles.rows} aria-label="Raízes e onde vive o state">
          {roots.map((root) => (
            <li key={root.folder} className={styles.row}>
              <div className={styles.rowHead}>
                <span className={styles.rowTitle}>
                  <code>{root.folder || 'raiz'}</code> · backend {root.backend}
                </span>
                <StatusChip tone={MODE[root.mode].tone}>{MODE[root.mode].label}</StatusChip>
              </div>
              <p className={styles.hint}>
                Trava {root.locking ? 'sim' : 'não'} · criptografia {root.encrypted ? 'sim' : 'não'}
                {Object.entries(root.settings).length > 0
                  ? ` · ${Object.entries(root.settings)
                      .map(([name, value]) => `${name}=${value}`)
                      .join(', ')}`
                  : ''}
              </p>
              {root.risks.map((risk) => (
                <p key={risk.rule} className={styles.hint}>
                  <StatusChip tone={LEVEL[risk.level]}>
                    {risk.level === 'danger' ? 'Grave' : risk.level === 'warning' ? 'Atenção' : 'Nota'}
                  </StatusChip>{' '}
                  {risk.message}
                </p>
              ))}
              <div className={styles.actions}>
                <Button
                  type="button"
                  variant="text"
                  disabled={!ready || action.busy}
                  onClick={() =>
                    void action.run(async () => {
                      const result = await api.stateResources({ projectId, folder: root.folder });
                      setRead({ ...result, folder: root.folder });
                    }, 'Não foi possível consultar o state.')
                  }
                >
                  Consultar o state
                </Button>
              </div>
              {read?.folder === root.folder ? (
                <div aria-label={`State de ${root.folder || 'raiz'}`}>
                  {read.error ? <p className={styles.error}>{read.error}</p> : null}
                  <p className={styles.hint}>
                    Workspaces: {read.workspaces.join(', ') || 'nenhum'} · {read.addresses.length} recurso(s) no{' '}
                    {read.workspace ?? 'default'}
                  </p>
                  <ul className={styles.impacts}>
                    {read.addresses.slice(0, 100).map((address) => (
                      <li key={address}>
                        <code>{address}</code>
                      </li>
                    ))}
                  </ul>
                </div>
              ) : null}
            </li>
          ))}
        </ul>
      )}
      {action.error ? <p className={styles.error}>{action.error}</p> : null}

      {recommendation ? <p>{recommendation.reason}</p> : null}

      {savedRun ? (
        <SuccessState>
          Arquivos do bootstrap gravados na execução {savedRun}, em <Link to={pathTo('runs')}>Execuções</Link> (dá para
          desfazer por lá). Próximo passo: validar, planejar e aprovar a pasta bootstrap nesta tela.
        </SuccessState>
      ) : plan ? (
        <section className={styles.consent} aria-label="Revisar o bootstrap">
          <p className={styles.rowTitle}>Revisar o bootstrap em {plan.folder}</p>
          {plan.changes.map((change) => (
            <div key={change.path}>
              <p className={styles.hint}>
                {change.path} · {change.op === 'create' ? 'arquivo novo' : 'alterado'}
              </p>
              <DiffView diff={change.diff} label={change.path} />
            </div>
          ))}
          <p className={styles.rowTitle}>Bloco backend para a raiz principal (depois que o bucket existir)</p>
          <DiffView diff={plan.backendBlock} label="bloco backend" />
          <ul className={styles.impacts}>
            {plan.notes.map((note) => (
              <li key={note}>{note}</li>
            ))}
          </ul>
          {confirming ? (
            <ConfirmAction
              label="Gravar os arquivos do bootstrap"
              confirmLabel="Gravar"
              busy={action.busy}
              onConfirm={() => {
                setConfirming(false);
                void action.run(
                  async () => setSavedRun(await api.bootstrapSave({ ...input(), planId: plan.planId })),
                  'Os arquivos não foram gravados.'
                );
              }}
              onCancel={() => setConfirming(false)}
            >
              Só arquivos vão para o projeto; nada é criado na nuvem agora.
            </ConfirmAction>
          ) : (
            <div className={styles.actions}>
              <Button type="button" disabled={action.busy} onClick={() => setConfirming(true)}>
                Gravar no projeto
              </Button>
              <Button type="button" variant="outlined" disabled={action.busy} onClick={() => setPlan(null)}>
                Voltar
              </Button>
            </div>
          )}
        </section>
      ) : (
        <section className={styles.consent} aria-label="Bootstrap do state">
          <p className={styles.rowTitle}>Bootstrap do bucket de state</p>
          <p className={styles.hint}>
            Gera infra/bootstrap no formato do modelo: o bucket com prevent_destroy, versões, criptografia e acesso
            público bloqueado, com state local fora do git. Você revisa os arquivos antes de gravar.
          </p>
          <fieldset className={styles.choices}>
            <legend>Nuvem</legend>
            {(['aws', 'azure', 'gcp'] as const).map((option) => (
              <Radio
                key={option}
                name="iac-bootstrap-provider"
                value={option}
                checked={provider === option}
                disabled={action.busy}
                onChange={() => {
                  setProvider(option);
                  setValues({});
                }}
                label={
                  option === 'aws' ? 'AWS (S3)' : option === 'azure' ? 'Azure (Storage Account)' : 'Google Cloud (GCS)'
                }
              />
            ))}
          </fieldset>
          {provider && linked === null ? <p className={styles.hint}>{LINKED_UNREADABLE}</p> : null}
          {connection ? (
            <Checkbox
              label={`Preencher conta, região e perfil pela conexão ${connection.name}`}
              checked={useConnection}
              disabled={action.busy}
              onChange={(_, checked) => setUseConnection(checked)}
            />
          ) : null}
          {provider
            ? FIELDS[provider].map((field) => (
                <TextField
                  key={`${provider}-${field.name}`}
                  label={field.label}
                  value={values[field.name] ?? ''}
                  disabled={action.busy}
                  helperText={field.hint}
                  onChange={(event) => setValues((current) => ({ ...current, [field.name]: event.target.value }))}
                  slotProps={{ htmlInput: { spellCheck: false } }}
                />
              ))
            : null}
          <div className={styles.actions}>
            <Button
              type="button"
              variant="outlined"
              disabled={!provider || action.busy}
              onClick={() =>
                void action.run(
                  async () => setPlan(await api.bootstrapPlan(input())),
                  'Não foi possível montar o bootstrap.'
                )
              }
            >
              Revisar os arquivos
            </Button>
          </div>
        </section>
      )}
    </div>
  );
}
