import { createCloudApi } from '@/features/cloud';
import { useAction, useReloadable } from '@/features/integrations';
import type { SelectedProject } from '@/features/project-selection';
import { errorMessage, type EngineClient } from '@/shared/api';
import { ErrorState, LoadingState, Page, PageSection, SuccessState } from '@/shared/components';
import { pathTo } from '@/shared/routing';
import type { TerraformOffer, TerraformSource } from '@/shared/schema';
import { useCallback, useMemo, useState } from 'react';
import { Link } from 'react-router';

import { LifecycleStrip } from './components/lifecycle-strip';
import { ProjectTerraform } from './components/project-terraform';
import { TerraformPanel } from './components/terraform-panel';
import styles from './infrastructure-page.module.scss';
import { createIacApi } from './service/iac-api';

export function InfrastructurePage({ client, project }: { client: EngineClient; project: SelectedProject | null }) {
  const api = useMemo(() => createIacApi(client), [client]);
  const cloud = useMemo(() => createCloudApi(client), [client]);
  const projectId = project?.projectId;
  // A consulta não fala com a rede: só olha a pasta do Spock e pergunta a versão ao terraform da máquina.
  const load = useCallback(async () => {
    const [terraform, connections] = await Promise.all([api.status(projectId), cloud.connections().catch(() => null)]);
    return { terraform, connections };
  }, [api, cloud, projectId]);
  const hub = useReloadable(load);
  const action = useAction(hub.reload);
  const [offer, setOffer] = useState<TerraformOffer | null>(null);

  const requestOffer = () =>
    void action.run(
      async () => setOffer(await api.offer(projectId ? { projectId } : {})),
      'Não foi possível consultar as versões publicadas pela HashiCorp.'
    );

  const install = (chosen: TerraformOffer) =>
    void action
      .run(() => api.install(chosen), 'O Terraform não foi instalado.', {
        success: () => `Terraform ${chosen.version} instalado e verificado.`
      })
      .then((ok) => ok && setOffer(null));

  const pin = (version: string | null, source?: TerraformSource) => {
    if (!projectId) return;
    void action.run(() => api.pin(projectId, version, source), 'Não foi possível trocar a versão do projeto.', {
      success: () => (version ? `Projeto fixado no Terraform ${version}.` : 'Projeto de volta à escolha automática.')
    });
  };

  const connected = hub.value?.connections?.filter((connection) => connection.status === 'connected').length ?? 0;

  return (
    <Page
      title="Infraestrutura"
      description="Gerencie infraestrutura como código com Terraform, junto das conexões cloud. O Spock separa o que só analisa do que prepara e do que executa: conectar uma conta não autoriza criar recursos, gerar arquivos não autoriza executá-los e um plano não autoriza sua aplicação."
    >
      {!hub.settled ? <LoadingState>Procurando o Terraform nesta máquina…</LoadingState> : null}
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

          <PageSection title="Dependência Terraform">
            {action.error ? <p className={styles.error}>{action.error}</p> : null}
            {action.notice ? <SuccessState>{action.notice.text}</SuccessState> : null}
            <TerraformPanel
              status={hub.value.terraform}
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
              onAdopt={(version) =>
                void action.run(() => api.adopt(version), 'Não foi possível usar o Terraform desta máquina.', {
                  success: () => `O Spock vai usar o Terraform ${version} desta máquina.`
                })
              }
              onRemove={(version) =>
                void action.run(() => api.remove(version), 'A versão não foi removida.', {
                  success: () => `Terraform ${version} removido da pasta do Spock.`
                })
              }
            />
          </PageSection>

          <PageSection title="Projeto">
            {project ? (
              <ProjectTerraform project={project} status={hub.value.terraform} busy={action.busy} onPin={pin} />
            ) : (
              <p className={styles.meta}>
                Selecione um projeto para ver o que os arquivos .tf exigem e qual versão ele usa.
              </p>
            )}
          </PageSection>

          <PageSection title="Provedores">
            <p className={styles.meta}>
              {hub.value.connections === null
                ? 'Não foi possível ler as conexões cloud.'
                : `${hub.value.connections.length} ${hub.value.connections.length === 1 ? 'conexão' : 'conexões'} cloud, ${connected} ${connected === 1 ? 'conectada' : 'conectadas'}. `}
              As conexões identificam contas e leem recursos; o Terraform declara e muda a infraestrutura. Uma não
              substitui a outra. <Link to={pathTo('cloud')}>Abrir Conexões cloud</Link>
            </p>
          </PageSection>
        </>
      ) : null}
    </Page>
  );
}
