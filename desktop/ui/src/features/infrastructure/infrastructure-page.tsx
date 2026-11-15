import { createCloudApi } from '@/features/cloud';
import { useAction, useReloadable } from '@/features/integrations';
import type { SelectedProject } from '@/features/project-selection';
import { errorMessage, type EngineClient } from '@/shared/api';
import { ErrorState, LoadingState, Page, PageSection, SuccessState } from '@/shared/components';
import { pathTo } from '@/shared/routing';
import type { IacEngine, IacRuntimeOffer, IacRuntimeSource } from '@/shared/schema';
import { useCallback, useMemo, useState } from 'react';
import { Link } from 'react-router';

import { IacOperations } from './components/iac-operations';
import { LifecycleStrip } from './components/lifecycle-strip';
import { ProjectRuntime } from './components/project-runtime';
import { RuntimePanel } from './components/runtime-panel';
import styles from './infrastructure-page.module.scss';
import { ENGINE_NAME } from './domain/labels';
import { createIacApi } from './service/iac-api';

export function InfrastructurePage({ client, project }: { client: EngineClient; project: SelectedProject | null }) {
  const api = useMemo(() => createIacApi(client), [client]);
  const cloud = useMemo(() => createCloudApi(client), [client]);
  const projectId = project?.projectId;
  // A consulta não fala com a rede: só olha a pasta do Spock e pergunta a versão ao tofu e ao terraform da máquina.
  const load = useCallback(async () => {
    const [runtime, connections] = await Promise.all([api.status(projectId), cloud.connections().catch(() => null)]);
    return { runtime, connections };
  }, [api, cloud, projectId]);
  const hub = useReloadable(load);
  const action = useAction(hub.reload);
  const [offer, setOffer] = useState<IacRuntimeOffer | null>(null);

  const requestOffer = (engine?: IacEngine) =>
    void action.run(
      async () => setOffer(await api.offer({ ...(projectId ? { projectId } : {}), ...(engine ? { engine } : {}) })),
      'Não foi possível consultar as versões publicadas.'
    );

  const install = (chosen: IacRuntimeOffer) =>
    void action
      .run(() => api.install(chosen), 'Nada foi instalado.', {
        success: () => `${chosen.engineName} ${chosen.version} instalado e verificado.`
      })
      .then((ok) => ok && setOffer(null));

  const pin = (choice: { engine: IacEngine; version: string; source: IacRuntimeSource } | null) => {
    if (!projectId) return;
    void action.run(() => api.pin(projectId, choice), 'Não foi possível trocar a ferramenta do projeto.', {
      success: () =>
        choice
          ? `Projeto fixado no ${ENGINE_NAME[choice.engine]} ${choice.version}.`
          : 'Projeto de volta à escolha automática.'
    });
  };

  const connected = hub.value?.connections?.filter((connection) => connection.status === 'connected').length ?? 0;

  return (
    <Page
      title="Infraestrutura"
      description="Gerencie infraestrutura como código com OpenTofu (gratuito, padrão) ou Terraform, junto das conexões cloud. O Spock separa o que só analisa do que prepara e do que executa: conectar uma conta não autoriza criar recursos, gerar arquivos não autoriza executá-los e um plano não autoriza sua aplicação."
    >
      {!hub.settled ? <LoadingState>Procurando OpenTofu e Terraform nesta máquina…</LoadingState> : null}
      {hub.settled && hub.value === null ? (
        <ErrorState onRetry={hub.reload}>
          {errorMessage(hub.error, 'Não foi possível ler o estado da infraestrutura.')}
        </ErrorState>
      ) : null}

      {hub.value ? (
        <>
          <PageSection title="Ciclo de uma mudança">
            <LifecycleStrip />
          </PageSection>

          <PageSection title="OpenTofu e Terraform">
            {action.error ? <p className={styles.error}>{action.error}</p> : null}
            {action.notice ? <SuccessState>{action.notice.text}</SuccessState> : null}
            <RuntimePanel
              status={hub.value.runtime}
              offer={offer}
              busy={action.busy}
              onRequestOffer={requestOffer}
              onDiscardOffer={() => setOffer(null)}
              onInstall={install}
              onDecline={() =>
                void action.run(() => api.decline(), 'Não foi possível registrar a recusa.', {
                  success: () =>
                    'Combinado: nada será instalado. A gestão de infraestrutura fica indisponível até você pedir.'
                })
              }
              onAdopt={(engine, version) =>
                void action.run(() => api.adopt(engine, version), 'Não foi possível usar a instalação desta máquina.', {
                  success: () => `O Spock vai usar o ${ENGINE_NAME[engine]} ${version} desta máquina.`
                })
              }
              onRemove={(engine, version) =>
                void action.run(() => api.remove(engine, version), 'A versão não foi removida.', {
                  success: () => `${ENGINE_NAME[engine]} ${version} removido da pasta do Spock.`
                })
              }
            />
          </PageSection>

          <PageSection title="Projeto">
            {project ? (
              <ProjectRuntime project={project} status={hub.value.runtime} busy={action.busy} onPin={pin} />
            ) : (
              <p className={styles.meta}>
                Selecione um projeto para ver o que os arquivos .tf exigem e qual versão ele usa.
              </p>
            )}
          </PageSection>

          {project ? (
            <PageSection title="Validar e planejar">
              <IacOperations
                key={project.projectId}
                client={client}
                api={api}
                project={project}
                ready={hub.value.runtime.project?.selected != null}
              />
            </PageSection>
          ) : null}

          <PageSection title="Provedores">
            <p className={styles.meta}>
              {hub.value.connections === null
                ? 'Não foi possível ler as conexões cloud.'
                : `${hub.value.connections.length} ${hub.value.connections.length === 1 ? 'conexão' : 'conexões'} cloud, ${connected} ${connected === 1 ? 'conectada' : 'conectadas'}. `}
              As conexões identificam contas e leem recursos; o OpenTofu (ou o Terraform) declara e muda a
              infraestrutura. Uma não substitui a outra. <Link to={pathTo('cloud')}>Abrir Conexões cloud</Link>
            </p>
          </PageSection>
        </>
      ) : null}
    </Page>
  );
}
