import { createCloudApi } from '@/features/cloud';
import { useAction, useReloadable } from '@/features/integrations';
import type { SelectedProject } from '@/features/project-selection';
import type { EngineClient } from '@/shared/api';
import { ConfirmAction, DiffView, Radio, StatusChip, SuccessState } from '@/shared/components';
import { pathTo } from '@/shared/routing';
import type { IacArchitectDraft, IacCostEstimate, IacGeneratePlan } from '@/shared/schema';
import { Button, Checkbox, TextField } from '@mariliz/ui-kit';
import { useCallback, useMemo, useState } from 'react';
import { Link } from 'react-router';

import styles from '../infrastructure-page.module.scss';
import {
  EMPTY_FORM,
  PROVIDER_DEFAULTS,
  SERVICE_SIZES,
  withProvider,
  type CloudProvider,
  fromBlueprint,
  fromConnection,
  fromProposal,
  toBlueprint,
  vpcsFromSubnets,
  type BlueprintForm,
  type DiscoveredVpc
} from '../domain/blueprint';
import type { IacApi } from '../service/iac-api';
import { CostEstimateView } from './cost-estimate';
import { SessionProposal } from './session-proposal';

const MAX_NEED = 4000;

// Nada nesta seção muda o que a página já mostra: o resultado de cada ação fica no próprio painel.
const NOTHING = () => undefined;

type TextKey = { [K in keyof BlueprintForm]: BlueprintForm[K] extends string ? K : never }[keyof BlueprintForm];

interface GeneratePanelProps {
  client: EngineClient;
  api: IacApi;
  project: SelectedProject;
}

/**
 * Arquitetura e configuração: a IA propõe componentes (opcional), a pessoa completa os fatos da conta, o motor confere e
 * gera os arquivos no formato do modelo, e só o diff aprovado vai para o projeto. Nada é criado na nuvem aqui.
 */
export function GeneratePanel({ client, api, project }: GeneratePanelProps) {
  const projectId = project.projectId;
  const cloud = useMemo(() => createCloudApi(client), [client]);
  const loadConnection = useCallback(async () => {
    const [links, connections] = await Promise.all([
      cloud.links(projectId).catch(() => []),
      cloud.connections().catch(() => [])
    ]);
    return links
      .map((link) => connections.find((connection) => connection.id === link.connectionId))
      .filter((connection) => connection !== undefined);
  }, [cloud, projectId]);
  const linked = useReloadable(loadConnection);
  const action = useAction(NOTHING);
  const [form, setForm] = useState<BlueprintForm>(EMPTY_FORM);
  const [need, setNeed] = useState('');
  const [draft, setDraft] = useState<IacArchitectDraft | null>(null);
  const [vpcs, setVpcs] = useState<DiscoveredVpc[] | null>(null);
  const [plan, setPlan] = useState<IacGeneratePlan | null>(null);
  const [confirming, setConfirming] = useState(false);
  const [savedRun, setSavedRun] = useState<string | null>(null);
  const [estimate, setEstimate] = useState<IacCostEstimate | null>(null);

  // A conexão vinculada da mesma nuvem do desenho; a estimativa de custo e a leitura de VPCs são só da AWS.
  const connection = (linked.value ?? []).find((candidate) => candidate.provider === form.provider) ?? null;
  const aws = form.provider === 'aws';
  const providerLabel = PROVIDER_DEFAULTS[form.provider].label;
  const set = <K extends keyof BlueprintForm>(key: K, value: BlueprintForm[K]) =>
    setForm((current) => ({ ...current, [key]: value }));
  const text = (key: TextKey, label: string, hint?: string) => (
    <TextField
      label={label}
      value={form[key]}
      disabled={action.busy}
      helperText={hint}
      onChange={(event) => set(key, event.target.value)}
      slotProps={{ htmlInput: { spellCheck: false } }}
    />
  );

  if (savedRun)
    return (
      <SuccessState>
        Arquivos gravados na execução {savedRun}, em <Link to={pathTo('runs')}>Execuções</Link> (dá para desfazer por
        lá). Próximo passo: validar, gerar o plano de um ambiente e aprovar, nesta tela.
      </SuccessState>
    );

  if (plan) {
    const failed = plan.checks.filter((check) => !check.passed);
    return (
      <section className={styles.consent} aria-label="Revisar a infraestrutura gerada">
        {failed.length > 0 ? (
          <>
            <p className={styles.rowTitle}>O desenho não passou nas verificações</p>
            <ul className={styles.impacts}>
              {failed.map((check) => (
                <li key={check.id}>
                  {check.description}
                  {check.detail ? ` (${check.detail})` : ''}
                </li>
              ))}
            </ul>
          </>
        ) : (
          <>
            <p className={styles.rowTitle}>
              Revisar {plan.changes.length} arquivo(s) em {plan.root}
            </p>
            {plan.changes.map((change) => (
              <div key={change.path}>
                <p className={styles.hint}>
                  {change.path} · {change.op === 'create' ? 'arquivo novo' : 'alterado'}
                </p>
                <DiffView diff={change.diff} label={change.path} />
              </div>
            ))}
            <p className={styles.rowTitle}>O que é cobrado (o preço depende da região e do uso)</p>
            <ul className={styles.impacts} aria-label="O que é cobrado">
              {plan.costs.map((cost) => (
                <li key={cost.resource}>
                  {cost.resource}: {cost.billing}
                </li>
              ))}
            </ul>
            {!aws ? null : estimate ? (
              <CostEstimateView estimate={estimate} />
            ) : connection ? (
              <div className={styles.actions}>
                <Button
                  type="button"
                  variant="outlined"
                  disabled={action.busy}
                  onClick={() =>
                    void action.run(
                      async () =>
                        setEstimate(
                          await api.costEstimate({
                            projectId,
                            connectionId: connection.id,
                            blueprint: toBlueprint(form)
                          })
                        ),
                      'Não foi possível consultar os preços pela conexão.'
                    )
                  }
                >
                  Estimar o custo com os preços da AWS
                </Button>
              </div>
            ) : (
              <p className={styles.hint}>
                Para estimar o custo, vincule uma conexão AWS ao projeto em{' '}
                <Link to={pathTo('cloud')}>Conexões cloud</Link> e libere nela a leitura de preços públicos.
              </p>
            )}
            {plan.notes.length > 0 ? (
              <ul className={styles.impacts} aria-label="Observações">
                {plan.notes.map((note) => (
                  <li key={note}>{note}</li>
                ))}
              </ul>
            ) : null}
          </>
        )}
        {action.error ? <p className={styles.error}>{action.error}</p> : null}
        {confirming ? (
          <ConfirmAction
            label="Gravar os arquivos de infraestrutura"
            confirmLabel="Gravar"
            busy={action.busy}
            onConfirm={() => {
              setConfirming(false);
              void action.run(
                async () =>
                  setSavedRun(await api.generateSave({ projectId, blueprint: toBlueprint(form), planId: plan.planId })),
                'Os arquivos não foram gravados.'
              );
            }}
            onCancel={() => setConfirming(false)}
          >
            Só arquivos vão para o projeto; nada é criado na nuvem agora.
          </ConfirmAction>
        ) : (
          <div className={styles.actions}>
            {failed.length === 0 ? (
              <Button type="button" disabled={action.busy} onClick={() => setConfirming(true)}>
                Gravar no projeto
              </Button>
            ) : null}
            <Button
              type="button"
              variant="outlined"
              disabled={action.busy}
              onClick={() => {
                setPlan(null);
                setEstimate(null);
              }}
            >
              Voltar ao desenho
            </Button>
          </div>
        )}
      </section>
    );
  }

  return (
    <div className={styles.panel} role="group" aria-label="Desenho da infraestrutura">
      <p className={styles.hint}>
        Gera a pasta de infraestrutura no formato do modelo do projeto: raiz com o workspace como ambiente e um módulo
        por serviço (contêiner com HTTPS, banco sem senha, filas com fila de mensagens mortas, alarmes). A IA só escolhe
        componentes e tamanhos; os arquivos saem de modelos fixos e você revisa o diff antes de gravar.
      </p>
      {action.error ? <p className={styles.error}>{action.error}</p> : null}

      <fieldset className={styles.choices}>
        <legend>Nuvem</legend>
        {(Object.keys(PROVIDER_DEFAULTS) as CloudProvider[]).map((option) => (
          <Radio
            key={option}
            name="iac-generate-provider"
            value={option}
            checked={form.provider === option}
            disabled={action.busy}
            onChange={() => {
              setForm((current) => withProvider(current, option));
              setVpcs(null);
              setDraft(null);
            }}
            label={PROVIDER_DEFAULTS[option].label}
          />
        ))}
      </fieldset>

      <SessionProposal
        api={api}
        projectId={projectId}
        busy={action.busy}
        onUse={(blueprint) => {
          setForm((current) => fromProposal(blueprint, current));
          setVpcs(null);
          setDraft(null);
        }}
      />

      {!aws ? (
        <p className={styles.hint}>
          A proposta com a IA do Spock é só para AWS por enquanto; no {providerLabel}, use o seu agente acima ou monte o
          desenho abaixo.
        </p>
      ) : null}
      <section className={styles.consent} aria-label="Propor com IA" hidden={!aws}>
        <p className={styles.rowTitle}>Propor o desenho com IA (opcional)</p>
        <TextField
          label="O que o sistema precisa"
          multiline
          minRows={3}
          fullWidth
          value={need}
          disabled={action.busy}
          onChange={(event) => setNeed(event.target.value)}
          helperText="Ex.: uma API de pedidos com banco relacional e uma fila para avisar o estoque. Ids da conta não vão para o modelo."
          slotProps={{ htmlInput: { maxLength: MAX_NEED } }}
        />
        <div className={styles.actions}>
          <Button
            type="button"
            variant="outlined"
            disabled={action.busy || need.trim().length < 20}
            onClick={() =>
              void action.run(async () => {
                const proposed = await api.architectDraft({ projectId, need, facts: toBlueprint(form) });
                setDraft(proposed);
                if (proposed.blueprint) setForm((current) => fromBlueprint(proposed.blueprint!, current));
              }, 'A IA não conseguiu propor o desenho. Monte à mão abaixo.')
            }
          >
            Propor com IA
          </Button>
        </div>
        {draft?.notes ? (
          <div aria-label="Por que este desenho">
            <ul className={styles.impacts}>
              {draft.notes.services.map((service) => (
                <li key={service.name}>
                  <strong>{service.name}</strong>: {service.reason}
                </li>
              ))}
            </ul>
            {draft.notes.risks.length > 0 ? (
              <p className={styles.hint}>Riscos: {draft.notes.risks.join(' · ')}</p>
            ) : null}
            {draft.notes.alternatives.length > 0 ? (
              <p className={styles.hint}>Alternativas: {draft.notes.alternatives.join(' · ')}</p>
            ) : null}
            {draft.notes.assumptions.length > 0 ? (
              <p className={styles.hint}>Premissas: {draft.notes.assumptions.join(' · ')}</p>
            ) : null}
            <p className={styles.hint}>
              {draft.attempts} tentativa(s), {draft.inputTokens + draft.outputTokens} tokens. Confira e complete os
              campos abaixo.
            </p>
          </div>
        ) : draft ? (
          <p className={styles.hint}>A resposta da IA não veio no formato esperado. Monte o desenho à mão abaixo.</p>
        ) : null}
      </section>

      <section className={styles.consent} aria-label="Conta e rede">
        <p className={styles.rowTitle}>Conta e rede existente</p>
        {connection ? (
          <div className={styles.actions}>
            <Button
              type="button"
              variant="text"
              disabled={action.busy}
              onClick={() => setForm((current) => fromConnection(connection, current))}
            >
              Preencher conta e região pela conexão {connection.name}
            </Button>
            {aws ? (
              <Button
                type="button"
                variant="text"
                disabled={action.busy || form.region.trim().length === 0}
                onClick={() =>
                  void action.run(async () => {
                    const read = await cloud.read({
                      connectionId: connection.id,
                      capability: 'aws.ec2.subnets',
                      region: form.region.trim(),
                      limit: 200
                    });
                    setVpcs(vpcsFromSubnets(read));
                  }, 'Não foi possível ler as subnets pela conexão.')
                }
              >
                Ler VPCs e subnets da conta
              </Button>
            ) : null}
          </div>
        ) : (
          <p className={styles.hint}>
            Vincule uma conexão {providerLabel} ao projeto em <Link to={pathTo('cloud')}>Conexões cloud</Link> para
            preencher conta e região sem digitar.
          </p>
        )}
        {vpcs ? (
          vpcs.length === 0 ? (
            <p className={styles.hint}>Nenhuma subnet nesta região.</p>
          ) : (
            <fieldset className={styles.choices}>
              <legend>VPC encontrada</legend>
              {vpcs.map((vpc) => (
                <Radio
                  key={vpc.id}
                  name="iac-generate-vpc"
                  value={vpc.id}
                  checked={form.vpcId === vpc.id}
                  disabled={action.busy}
                  onChange={() =>
                    setForm((current) => ({
                      ...current,
                      vpcId: vpc.id,
                      privateSubnets: vpc.privateSubnets.join(', '),
                      publicSubnets: vpc.publicSubnets.join(', ')
                    }))
                  }
                  label={`${vpc.id} · ${vpc.privateSubnets.length} privada(s), ${vpc.publicSubnets.length} pública(s)`}
                />
              ))}
            </fieldset>
          )
        ) : null}
        {text('project', 'Nome do projeto', 'Minúsculas, dígitos e hífen, até 18 caracteres (ex.: loja-api).')}
        {text('region', 'Região', `Ex.: ${PROVIDER_DEFAULTS[form.provider].region}.`)}
        {aws ? (
          <>
            {text('account', 'Conta (opcional)', 'Vira allowed_account_ids: o provider recusa outra conta.')}
            {text('profile', 'Perfil SSO (opcional)', 'Sem ele, vale a cadeia padrão de credenciais.')}
            {text('vpcId', 'VPC existente', 'O Terraform não cria rede.')}
            {text('privateSubnets', 'Subnets privadas', 'Separadas por vírgula; o banco precisa de duas zonas.')}
            {text('publicSubnets', 'Subnets públicas', 'Para o load balancer, em duas zonas.')}
          </>
        ) : form.provider === 'azure' ? (
          <>
            {text('account', 'Subscription', 'GUID da subscription; o provider opera só nela.')}
            {text('profile', 'Tenant (opcional)', 'GUID do tenant do Entra ID; sem ele, vale o da credencial.')}
            {form.withDatabase ? (
              <>
                {text(
                  'privateSubnets',
                  'Subnet delegada ao PostgreSQL',
                  'Id completo da subnet; o Terraform não cria rede.'
                )}
                {text(
                  'privateDnsZone',
                  'Zona DNS privada do PostgreSQL',
                  'Id completo de *.postgres.database.azure.com.'
                )}
              </>
            ) : null}
          </>
        ) : (
          <>
            {text('account', 'Projeto do Google Cloud', 'Ex.: loja-producao-123.')}
            {form.withDatabase
              ? text(
                  'vpcId',
                  'Rede VPC do banco',
                  'projects/<projeto>/global/networks/<rede>, com acesso privado a serviços.'
                )
              : null}
          </>
        )}
      </section>

      <section className={styles.consent} aria-label="Componentes">
        <p className={styles.rowTitle}>Componentes</p>
        <Checkbox
          label={
            aws
              ? 'Serviço em contêiner (ECS Fargate atrás de load balancer só HTTPS)'
              : form.provider === 'azure'
                ? 'Serviço em contêiner (Container Apps com HTTPS gerenciado)'
                : 'Serviço em contêiner (Cloud Run com HTTPS gerenciado)'
          }
          checked={form.withService}
          disabled={action.busy}
          onChange={(_, checked) => set('withService', checked)}
        />
        {form.withService ? (
          <>
            {text('serviceName', 'Nome do serviço', 'Até 10 caracteres.')}
            {text('port', 'Porta do contêiner')}
            {text('healthPath', 'Caminho do health check')}
            <fieldset className={styles.choices}>
              <legend>CPU e memória</legend>
              {SERVICE_SIZES[form.provider].map((size) => (
                <Radio
                  key={size}
                  name="iac-generate-size"
                  value={size}
                  checked={form.size === size}
                  disabled={action.busy}
                  onChange={() => set('size', size)}
                  label={`${size.split('/')[0]} CPU, ${size.split('/')[1]} MB`}
                />
              ))}
            </fieldset>
            {text('desiredCount', aws ? 'Tarefas rodando' : 'Réplicas mínimas')}
            {aws ? (
              <>
                {text('certificateArn', 'Certificado ACM (ARN)', 'Emitido na mesma região do load balancer.')}
                <Checkbox
                  label="Tarefas com IP público (subnets públicas, sem NAT)"
                  checked={form.publicIp}
                  disabled={action.busy}
                  onChange={(_, checked) => set('publicIp', checked)}
                />
              </>
            ) : null}
          </>
        ) : null}
        <Checkbox
          label={
            aws
              ? 'Banco RDS (senha criada e guardada pelo próprio RDS)'
              : form.provider === 'azure'
                ? 'Banco PostgreSQL Flexible Server (só login do Entra ID, sem senha)'
                : 'Banco Cloud SQL (só IP privado e login IAM, sem senha)'
          }
          checked={form.withDatabase}
          disabled={action.busy}
          onChange={(_, checked) => set('withDatabase', checked)}
        />
        {form.withDatabase ? (
          <>
            <fieldset className={styles.choices}>
              <legend>Motor</legend>
              {(form.provider === 'azure' ? (['postgres'] as const) : (['postgres', 'mysql'] as const)).map(
                (engine) => (
                  <Radio
                    key={engine}
                    name="iac-generate-engine"
                    value={engine}
                    checked={form.engine === engine}
                    disabled={action.busy}
                    onChange={() => set('engine', engine)}
                    label={engine === 'postgres' ? 'PostgreSQL' : 'MySQL'}
                  />
                )
              )}
            </fieldset>
            {text('engineVersion', 'Versão principal', 'Ex.: 16.')}
            {text(
              'instanceClass',
              aws ? 'Classe da instância' : form.provider === 'azure' ? 'SKU' : 'Tier',
              `Ex.: ${PROVIDER_DEFAULTS[form.provider].instanceClass}.`
            )}
            {text(
              'storageGb',
              'Armazenamento (GB)',
              aws
                ? 'Entre 20 e 1000.'
                : form.provider === 'azure'
                  ? '32, 64, 128, 256, 512 ou 1024.'
                  : 'Entre 10 e 1000.'
            )}
            <Checkbox
              label={aws ? 'Multi-AZ (réplica em outra zona)' : 'Alta disponibilidade (réplica em outra zona)'}
              checked={form.multiAz}
              disabled={action.busy}
              onChange={(_, checked) => set('multiAz', checked)}
            />
          </>
        ) : null}
        {text(
          'queues',
          aws ? 'Filas SQS' : form.provider === 'azure' ? 'Filas do Service Bus' : 'Fluxos do Pub/Sub',
          'Uma por fluxo, separadas por vírgula (ex.: pedidos, estoque).'
        )}
        {text('alarmEmail', 'E-mail dos alarmes (opcional)')}
        {text(
          'bucket',
          form.provider === 'azure' ? 'Storage account de state (opcional)' : 'Bucket de state (opcional)',
          'O do bootstrap. Sem ele, a raiz nasce sem backend remoto.'
        )}
        {form.provider === 'azure' && form.bucket.trim() ? (
          <>
            {text('bucketResourceGroup', 'Grupo de recursos da storage account')}
            {text('bucketContainer', 'Container do state')}
          </>
        ) : null}
      </section>

      {draft && !draft.valid ? (
        <p className={styles.hint}>
          <StatusChip tone="warning">Falta completar</StatusChip>{' '}
          {draft.checks
            .filter((check) => !check.passed)
            .map((check) => check.description)
            .join(' · ')}
        </p>
      ) : null}
      <div className={styles.actions}>
        <Button
          type="button"
          variant="outlined"
          disabled={action.busy}
          onClick={() =>
            void action.run(
              async () => setPlan(await api.generatePlan({ projectId, blueprint: toBlueprint(form) })),
              'Não foi possível montar os arquivos.'
            )
          }
        >
          Revisar os arquivos
        </Button>
      </div>
    </div>
  );
}
