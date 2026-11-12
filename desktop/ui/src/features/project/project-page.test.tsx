import harnessFixture from '@contracts/fixtures/engineering/harness.json';
import knowledgeFixture from '@contracts/fixtures/engineering/knowledge.json';
import revisionsFixture from '@contracts/fixtures/engineering/revisions.json';
import profileFixture from '@contracts/fixtures/engineering/profile.json';

import type { EngineClient } from '@/shared/api';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, useLocation } from 'react-router';
import { describe, expect, it, vi } from 'vitest';

import { HarnessPage } from './harness-page';
import { KnowledgePage } from './knowledge-page';
import { ProjectPage } from './project-page';

const project = { projectId: 'p-aaaaaaaaaaaa', displayName: 'Loja', displayPath: '~\\Loja' };

const item = (name: string, managed = true) => ({ name, path: `.claude/${name}`, managed, issue: null });

const discovery = {
  projectId: project.projectId,
  projectName: 'Loja',
  stacks: [{ id: 'dotnet', name: '.NET', root: '.', version: '10.0', confidence: 'high', evidence: [], notes: [] }],
  tooling: [{ kind: 'test', name: 'dotnet test', command: 'dotnet test', evidence: [] }],
  aiEnvironments: [],
  inventory: { agents: [item('revisor', false)], skills: [], hooks: [], rules: [], mcpServers: [], evals: [] },
  specification: {
    state: 'not_installed',
    version: null,
    lockedHash: null,
    computedHash: null,
    versionTested: false,
    problems: []
  },
  git: { isRepository: true, hasUncommittedChanges: false, changedPaths: [] },
  tools: { git: null, node: null, python: null, pythonCommand: null },
  hasDeclaredConfig: false,
  warnings: []
};

/** O perfil salvo começa vazio quando `saved` é falso: a tela precisa pedir ao motor que crie o primeiro. */
function clientWith(runs: unknown[], saved = true): EngineClient {
  let stored: unknown = saved ? profileFixture.profile : null;
  const refreshed = {
    ...profileFixture.profile,
    analyzedAt: '2026-11-12T12:00:00+00:00',
    history: [
      { at: '2026-11-12T12:00:00+00:00', kind: 'changed', subject: 'Next.js', from: '16.0.1', to: '16.1.0' },
      ...profileFixture.profile.history
    ]
  };
  return {
    request: vi.fn(async (method: string, _params: unknown, schema: { parse(value: unknown): unknown }) => {
      if (method === 'harness.runs') return schema.parse({ runs });
      if (method === 'engineering.profile') return schema.parse({ profile: stored });
      if (method === 'engineering.knowledge') return schema.parse(knowledgeFixture);
      if (method === 'engineering.harness') return schema.parse(harnessFixture);
      if (method === 'engineering.revisions') return schema.parse(revisionsFixture);
      if (method === 'engineering.analyze') {
        stored = stored === null ? profileFixture.profile : refreshed;
        return schema.parse({ profile: stored });
      }
      return schema.parse(discovery);
    }),
    onEvent: vi.fn()
  } as unknown as EngineClient;
}

/** Mostra para onde a página navegou e com que estado. */
function LocationProbe() {
  const location = useLocation();
  return <output aria-label="rota">{`${location.pathname} ${JSON.stringify(location.state)}`}</output>;
}

const renderPage = (
  client: EngineClient,
  selected: typeof project | null = project,
  Screen: typeof ProjectPage = ProjectPage
) =>
  render(
    <MemoryRouter>
      <Screen client={client} project={selected} />
      <LocationProbe />
    </MemoryRouter>
  );

describe('Projeto · visão geral', () => {
  it('mostra o inventário, a stack e as execuções reais do projeto', async () => {
    const client = clientWith([
      {
        runId: 'run-aaaaaaaaaaaa',
        kind: 'apply',
        state: 'completed',
        startedAt: '2026-11-04T10:00:00Z',
        changesApplied: true,
        reverted: false
      }
    ]);
    renderPage(client);

    expect(await screen.findByText('revisor')).toBeTruthy();
    expect(screen.getByText(/não gerenciado/)).toBeTruthy();
    const profile = (await screen.findByRole('heading', { name: 'Perfil tecnológico' })).closest('section')!;
    const technologies = await within(profile).findByRole('list', { name: 'Tecnologias' });
    expect(technologies).toHaveTextContent('Next.js');
    expect(technologies).toHaveTextContent('Framework web');
    expect(within(profile).getByRole('list', { name: 'Ferramentas' })).toHaveTextContent('pnpm run test');
    expect(within(profile).getByText(/Regra da stack JavaScript e TypeScript/)).toBeTruthy();
    expect(within(profile).getByText(/Nenhuma ferramenta externa vinculada/)).toBeTruthy();
    expect(screen.getByText('Configuração aplicada')).toBeTruthy();
    expect(screen.getByText(/alterações aplicadas/)).toBeTruthy();
    expect(client.request).toHaveBeenCalledWith('harness.runs', { projectId: project.projectId }, expect.anything());
  });

  it('sem execuções, diz isso em vez de mostrar uma lista vazia', async () => {
    renderPage(clientWith([]));
    expect(await screen.findByText('Nenhuma execução neste projeto ainda.')).toBeTruthy();
  });

  it('erro do motor vira aviso, não tela quebrada', async () => {
    const client = {
      request: vi.fn(async () => Promise.reject(new Error('falhou'))),
      onEvent: vi.fn()
    } as unknown as EngineClient;
    renderPage(client);
    expect((await screen.findByRole('alert')).textContent).toBeTruthy();
  });

  it('sem perfil salvo, pede ao motor que crie o primeiro; atualizar mostra o que mudou', async () => {
    const client = clientWith([], false);
    renderPage(client);

    const profile = (await screen.findByRole('heading', { name: 'Perfil tecnológico' })).closest('section')!;
    expect(await within(profile).findByRole('list', { name: 'Tecnologias' })).toHaveTextContent('Next.js');
    expect(client.request).toHaveBeenCalledWith(
      'engineering.analyze',
      { projectId: project.projectId },
      expect.anything()
    );

    await userEvent.click(within(profile).getByRole('button', { name: 'Atualizar perfil' }));
    await userEvent.click(await within(profile).findByText(/Histórico de mudanças \(2\)/));
    expect(within(profile).getByText('Next.js mudou de 16.0.1 para 16.1.0.')).toBeTruthy();
    expect(within(profile).getByText('Perfil criado com 1 tecnologia.')).toBeTruthy();
  });

  it('não repete o Harness, as revisões nem o conhecimento: cada um tem a sua tela, e a visão geral aponta para elas', async () => {
    renderPage(clientWith([]));

    await screen.findByRole('heading', { name: 'Perfil tecnológico' });
    expect(screen.queryByRole('heading', { name: 'Harness aplicado' })).toBeNull();
    expect(screen.queryByRole('heading', { name: 'Revisões do Harness' })).toBeNull();
    expect(screen.queryByRole('heading', { name: 'Conhecimento do projeto' })).toBeNull();
    const more = screen.getByRole('region', { name: 'Mais sobre o projeto' });
    expect(within(more).getByRole('link', { name: 'Harness' })).toHaveAttribute('href', '/projeto/harness');
    expect(within(more).getByRole('link', { name: 'Conhecimento' })).toHaveAttribute('href', '/projeto/conhecimento');
  });

  it('sem projeto selecionado, manda escolher na tela inicial', () => {
    renderPage(clientWith([]), null);
    expect(screen.getByText(/Nenhum projeto selecionado/)).toBeTruthy();
  });
});

describe('Projeto · conhecimento', () => {
  it('mostra o conhecimento do projeto: o que entra, o que fica de fora e o estado no índice', async () => {
    renderPage(clientWith([]), project, KnowledgePage);

    const knowledge = (await screen.findByRole('heading', { name: 'Conhecimento do projeto' })).closest('section')!;
    expect(await within(knowledge).findByText(/3 arquivos podem alimentar o contexto dos agentes/)).toBeTruthy();
    expect(
      within(knowledge).getByText(/No índice: 1 indexado, 1 mudou desde a indexação, 1 ainda não indexado\./)
    ).toBeTruthy();
    expect(within(knowledge).getByText('docs/rascunho/')).toBeTruthy();
    const excluded = within(knowledge).getByRole('list', { name: 'Arquivos fora do conhecimento' });
    expect(excluded).toHaveTextContent('Possível segredo no conteúdo');
    expect(excluded).toHaveTextContent('src/Config.cs');

    await userEvent.click(within(knowledge).getByText(/Arquivos que entram \(3\)/));
    expect(within(knowledge).getByText('src/App.cs').closest('li')).toHaveTextContent(
      'src/App.cs (mudou desde a indexação)'
    );
  });

  it('sem projeto selecionado, manda escolher na tela inicial', () => {
    renderPage(clientWith([]), null, KnowledgePage);
    expect(screen.getByText(/Nenhum projeto selecionado/)).toBeTruthy();
  });
});

describe('Projeto · Harness', () => {
  it('mostra o Harness aplicado e aponta o componente modificado à mão, com o arquivo', async () => {
    renderPage(clientWith([]), project, HarnessPage);

    const harness = (await screen.findByRole('heading', { name: 'Harness aplicado' })).closest('section')!;
    expect(await within(harness).findByText(/8 componentes: 7 íntegros, 1 modificado à mão, 0 ausentes/)).toBeTruthy();
    const list = within(harness).getByRole('list', { name: 'Componentes do Harness' });
    const first = within(list).getAllByRole('listitem')[0]!;
    expect(first).toHaveTextContent('Agente revisor de código');
    expect(first).toHaveTextContent('Modificado à mão');
    expect(first).toHaveTextContent('.claude/agents/code-reviewer.md');
    expect(within(harness).getByText(/Nada é aplicado sem aprovação/)).toBeTruthy();
  });

  it('dispensar um componente abre o Configurar com o pedido, sem mudar nada antes do plano', async () => {
    const client = clientWith([]);
    renderPage(client, project, HarnessPage);

    await userEvent.click(await screen.findByRole('button', { name: 'Dispensar Agente revisor de código' }));

    const route = screen.getByRole('status', { name: 'rota' }).textContent ?? '';
    expect(route.startsWith('/configurar ')).toBe(true);
    expect(JSON.parse(route.slice('/configurar '.length))).toEqual({
      componentRequest: {
        exclude: [{ id: 'mariliz.agent.code-reviewer', label: 'Agente revisor de código' }],
        include: []
      }
    });
    expect(client.request).not.toHaveBeenCalledWith('harness.plan', expect.anything(), expect.anything());
  });

  it('lista as revisões do Harness, com a reversão ligada à revisão que ela desfez', async () => {
    renderPage(clientWith([]), project, HarnessPage);

    const list = await screen.findByRole('list', { name: 'Revisões do Harness' });
    const [revert, dispense, first] = within(list)
      .getAllByRole('listitem')
      .filter((item) => item.parentElement === list);
    expect(revert).toHaveTextContent('Revisão 3');
    expect(revert).toHaveTextContent('reversão da revisão 2');
    expect(revert).toHaveTextContent('Agente revisor de código: criou 1 arquivo');
    expect(dispense).toHaveTextContent('Revertida na revisão 3');
    expect(dispense).toHaveTextContent('Agente revisor de código: removeu 1 arquivo');
    expect(first).toHaveTextContent('Revisão 1');
  });

  it('não traz o inventário nem o conhecimento: só o que foi aplicado e como mudou', async () => {
    renderPage(clientWith([]), project, HarnessPage);

    await screen.findByRole('heading', { name: 'Harness aplicado' });
    expect(screen.queryByRole('heading', { name: 'Conhecimento do projeto' })).toBeNull();
    expect(screen.queryByRole('heading', { name: 'O que já existe' })).toBeNull();
  });

  it('sem projeto selecionado, manda escolher na tela inicial', () => {
    renderPage(clientWith([]), null, HarnessPage);
    expect(screen.getByText(/Nenhum projeto selecionado/)).toBeTruthy();
  });
});
