import { createCloudApi } from '@/features/cloud';
import type { SelectedProject } from '@/features/project-selection';
import type { EngineClient } from '@/shared/api';
import { Radio, ResourceView, StatusChip, SuccessState } from '@/shared/components';
import { useAsyncResource } from '@/shared/hooks';
import { pathTo } from '@/shared/routing';
import type { CloudConnection, IacProviderOptions } from '@/shared/schema';
import { Button } from '@mariliz/ui-kit';
import { useCallback, useMemo } from 'react';
import { Link } from 'react-router';

import styles from '../infrastructure-page.module.scss';
import { isProvider } from '../domain/provider-fields';
import { useGenerateFlow } from '../hooks/use-generate-flow';
import type { IacApi } from '../service/iac-api';
import { AccountSection } from './account-section';
import { ArchitectDraftSection } from './architect-draft-section';
import { ComponentsSection } from './components-section';
import { GenerateReview } from './generate-review';
import { SessionProposal } from './session-proposal';

interface GeneratePanelProps {
  client: EngineClient;
  api: IacApi;
  project: SelectedProject;
  /** Lidas uma vez pela página; `null` quando a leitura falhou. */
  linked: CloudConnection[] | null;
}

/**
 * Arquitetura e configuração: a IA propõe componentes (opcional), a pessoa completa os fatos da conta, o motor confere e
 * gera os arquivos no formato do modelo, e só o diff aprovado vai para o projeto. Nada é criado na nuvem aqui. O que cada
 * nuvem oferece vem do motor antes do formulário.
 */
export function GeneratePanel(props: GeneratePanelProps) {
  const { api } = props;
  const load = useCallback(
    async () => (await api.generateOptions()).filter((provider) => isProvider(provider.id)),
    [api]
  );
  const options = useAsyncResource(load);
  return (
    <ResourceView
      resource={options}
      loading="Lendo o que cada nuvem oferece…"
      fallbackError="Não foi possível ler as opções do desenho."
    >
      {(providers) => <GenerateFlowView {...props} providers={providers} />}
    </ResourceView>
  );
}

function GenerateFlowView({
  client,
  api,
  project,
  linked,
  providers
}: GeneratePanelProps & { providers: IacProviderOptions[] }) {
  const projectId = project.projectId;
  const cloud = useMemo(() => createCloudApi(client), [client]);
  const flow = useGenerateFlow({ api, cloud, projectId, providers });
  const { state, options, busy } = flow;
  // A conexão vinculada da mesma nuvem do desenho.
  const connection = (linked ?? []).find((candidate) => candidate.provider === state.form.provider) ?? null;

  if (state.savedRun)
    return (
      <SuccessState>
        Arquivos gravados na execução {state.savedRun}, em <Link to={pathTo('runs')}>Execuções</Link> (dá para desfazer
        por lá). Próximo passo: validar, gerar o plano de um ambiente e aprovar, nesta tela.
      </SuccessState>
    );

  if (state.plan) return <GenerateReview flow={flow} plan={state.plan} connection={connection} linked={linked} />;

  const drafting = providers.filter((provider) => provider.architectDraft).map((provider) => provider.label);
  return (
    <div className={styles.panel} role="group" aria-label="Desenho da infraestrutura">
      <p className={styles.hint}>
        Gera a pasta de infraestrutura no formato do modelo do projeto: raiz com o workspace como ambiente e um módulo
        por serviço (contêiner com HTTPS, banco sem senha, filas com fila de mensagens mortas, alarmes). A IA só escolhe
        componentes e tamanhos; os arquivos saem de modelos fixos e você revisa o diff antes de gravar.
      </p>
      {flow.error ? <p className={styles.error}>{flow.error}</p> : null}

      <fieldset className={styles.choices}>
        <legend>Nuvem</legend>
        {providers.map((provider) => (
          <Radio
            key={provider.id}
            name="iac-generate-provider"
            value={provider.id}
            checked={state.form.provider === provider.id}
            disabled={busy}
            onChange={() => flow.dispatch({ type: 'provider', options: provider })}
            label={provider.label}
          />
        ))}
      </fieldset>

      <SessionProposal
        api={api}
        projectId={projectId}
        busy={busy}
        onUse={(blueprint) => flow.dispatch({ type: 'proposal', blueprint, providers })}
      />

      {!options.architectDraft ? (
        <p className={styles.hint}>
          A proposta com a IA do Spock é só para {drafting.join(', ')} por enquanto; no {options.label}, use o seu
          agente acima ou monte o desenho abaixo.
        </p>
      ) : null}
      <ArchitectDraftSection flow={flow} hidden={!options.architectDraft} />
      <AccountSection flow={flow} connection={connection} linked={linked} />
      <ComponentsSection flow={flow} />

      {state.draft && !state.draft.valid ? (
        <p className={styles.hint}>
          <StatusChip tone="warning">Falta completar</StatusChip>{' '}
          {state.draft.checks
            .filter((check) => !check.passed)
            .map((check) => check.description)
            .join(' · ')}
        </p>
      ) : null}
      <div className={styles.actions}>
        <Button type="button" variant="outlined" disabled={busy} onClick={() => void flow.review()}>
          Revisar os arquivos
        </Button>
      </div>
    </div>
  );
}
