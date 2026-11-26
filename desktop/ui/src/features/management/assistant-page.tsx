import type { SelectedProject } from '@/features/project-selection';
import type { EngineClient } from '@/shared/api';
import { NoProjectState, Page, ResourceView, Term } from '@/shared/components';
import { useAsyncResource } from '@/shared/hooks';
import { readHandoff } from '@/shared/routing';
import type { PortfolioProject } from '@/shared/schema';
import { useCallback, useMemo, useState } from 'react';
import { useLocation } from 'react-router';

import { ChoiceGroup, type Choice } from './components/choice-group';
import { ModelKeyNotice } from './components/model-key';
import { PortfolioQuestion } from './components/portfolio-question';
import { ProjectQuestion } from './components/project-question';
import { MAX_QUESTION } from './domain/assistant';
import { createManagementApi, type ManagementApi } from './service/management-api';

type Scope = 'project' | 'portfolio';

function scopeChoices(project: SelectedProject | null, others: number): Choice<Scope>[] {
  return [
    {
      value: 'project',
      label: project ? `Este projeto (${project.displayName})` : 'Este projeto',
      note: project
        ? 'Responde com os fatos dele; também pode investigar em etapas, sem modelo.'
        : 'Nenhum projeto aberto: escolha um no seletor do topo.',
      disabled: !project
    },
    {
      value: 'portfolio',
      label: 'Vários projetos',
      note:
        others > 1
          ? 'Cruza os projetos que você escolher; precisa de uma chave de modelo.'
          : 'Só há um projeto aberto neste computador; abra outro para comparar.',
      disabled: others < 2
    }
  ];
}

/** Uma tela para perguntar: sobre o projeto aberto ou sobre vários. A pergunta digitada fica ao trocar o escopo. */
export function AssistantPage({ client, project }: { client: EngineClient; project: SelectedProject | null }) {
  const api = useMemo(() => createManagementApi(client), [client]);
  const load = useCallback(async () => {
    const [status, portfolio] = await Promise.all([api.modelStatus(), api.portfolio()]);
    return { configured: status.configured, projects: portfolio.projects };
  }, [api]);
  const context = useAsyncResource(load);

  return (
    <Page
      title="Assistente"
      description="Pergunte sobre andamento, riscos e previsão. A resposta separa o que foi observado, o que foi calculado, o que é interpretação e o que não está disponível."
    >
      <ResourceView
        resource={context}
        loading="Preparando o assistente…"
        fallbackError="Não foi possível abrir o assistente."
      >
        {({ configured, projects }) =>
          !project && projects.length < 2 ? (
            <NoProjectState />
          ) : (
            <Assistant
              key={project?.projectId ?? 'none'}
              api={api}
              project={project}
              projects={projects}
              configured={configured}
            />
          )
        }
      </ResourceView>
    </Page>
  );
}

interface AssistantProps {
  api: ManagementApi;
  project: SelectedProject | null;
  projects: PortfolioProject[];
  configured: boolean;
}

function Assistant({ api, project, projects, configured }: AssistantProps) {
  const { state } = useLocation();
  const [question, setQuestion] = useState(() => readHandoff(state)?.slice(0, MAX_QUESTION) ?? '');
  const [scope, setScope] = useState<Scope>(project ? 'project' : 'portfolio');

  return (
    <>
      {!configured ? (
        <ModelKeyNotice>
          O assistente usa um modelo de linguagem para escrever a resposta. Sem ele, só a{' '}
          <Term id="stepInvestigation" /> de um projeto responde, e lista os sinais.
        </ModelKeyNotice>
      ) : null}
      <ChoiceGroup
        legend="Sobre o que perguntar"
        name="assistant-scope"
        value={scope}
        choices={scopeChoices(project, projects.length)}
        onChange={setScope}
      />
      {scope === 'project' && project ? (
        <ProjectQuestion
          api={api}
          project={project}
          configured={configured}
          question={question}
          onQuestion={setQuestion}
        />
      ) : (
        <PortfolioQuestion
          api={api}
          projects={projects}
          configured={configured}
          question={question}
          onQuestion={setQuestion}
        />
      )}
    </>
  );
}
