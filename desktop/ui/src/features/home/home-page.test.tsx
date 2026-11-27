import bootAnswered from '@contracts/fixtures/boot/start-answered.json';
import bootClarify from '@contracts/fixtures/boot/start-clarify.json';
import bootHandoff from '@contracts/fixtures/boot/start-handoff.json';
import bootList from '@contracts/fixtures/boot/list.json';
import bootProject from '@contracts/fixtures/boot/project.json';
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, useLocation } from 'react-router';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { ProjectSelectionProvider, type ProjectPickerPort } from '@/features/project-selection';
import type { EngineClient } from '@/shared/api';
import { AppProviders } from '@/shared/components';
import { readHandoff } from '@/shared/routing';
import { bootListSchema } from '@/shared/schema';
import { expectNoA11yViolations } from '@/test/a11y';

import { HomePage } from './home-page';

const invoke = vi.fn();
vi.mock('@tauri-apps/api/core', () => ({ invoke: (...args: unknown[]) => invoke(...args) }));
vi.mock('@tauri-apps/api/event', () => ({ listen: vi.fn().mockResolvedValue(() => undefined) }));

const project = { projectId: 'p-1', displayName: 'loja-api', displayPath: '~\\loja-api', recentId: 'r-1' };
const recent = { id: 'r-1', displayName: 'loja-api', displayPath: '~\\loja-api', lastOpenedMs: 1, available: true };

const run = (runId: string, state: string) => ({
  runId,
  projectId: 'p-1',
  projectName: 'loja-api',
  kind: 'validate',
  state,
  startedAt: '2026-11-20T10:00:00Z',
  finishedAt: null,
  live: false,
  interrupted: false,
  changesApplied: false,
  reverted: false,
  canRevert: false,
  revertsRunId: null,
  error: null,
  operations: 0,
  pending: 0,
  notApplied: 0,
  tokens: null
});

/** O motor de mentira responde só o que o início pergunta. */
interface Facts {
  model?: boolean;
  connections?: number;
  applied?: boolean;
  runs?: unknown[];
  /** Há uma ferramenta de IA instalada no computador. */
  agent?: boolean;
  /** O Boot do motor: devolve a resposta (já no formato do contrato) ou lança, como o motor faria. */
  boot?: (method: string, params: Record<string, unknown>) => unknown;
  /** Os Boots anteriores do projeto, no formato do contrato; sem isso a lista vem vazia e o bloco some. */
  bootList?: () => unknown;
}

function engine(facts: Facts): EngineClient {
  return {
    request: vi.fn(async (method: string, params: Record<string, unknown>, schema) => {
      if (method === 'model.status') return schema.parse({ configured: facts.model ?? false, provider: 'anthropic' });
      if (method === 'connection.list') return { connections: Array(facts.connections ?? 0).fill({}) } as never;
      if (method === 'aitool.list')
        return schema.parse({
          tools: [
            {
              id: 'claude-code',
              name: 'Claude Code',
              binary: 'claude',
              installed: facts.agent ?? false,
              version: null,
              harnessSupport: true
            }
          ]
        });
      if (method === 'engineering.harness') return { applied: facts.applied ?? false } as never;
      if (method === 'run.list')
        return schema.parse({
          runs: params.states ? [] : (facts.runs ?? []),
          nextCursor: null,
          summary: { total: 0, running: 0, awaitingApproval: 0 }
        });
      if (method === 'boot.list') return schema.parse(facts.bootList ? facts.bootList() : { boots: [] });
      if (method.startsWith('boot.') && facts.boot) return schema.parse(facts.boot(method, params));
      throw new Error(`método inesperado: ${method}`);
    }) as EngineClient['request'],
    onEvent: vi.fn().mockResolvedValue(() => undefined)
  };
}

function Where() {
  const { pathname, state } = useLocation();
  return (
    <>
      <p data-testid="where">{pathname}</p>
      <p data-testid="handoff">{readHandoff(state) ?? ''}</p>
    </>
  );
}

interface Options {
  picker?: Partial<ProjectPickerPort>;
  client?: EngineClient;
  findShortcuts?: Parameters<typeof HomePage>[0]['findShortcuts'];
}

function renderHome({ picker = {}, client = engine({}), findShortcuts }: Options = {}) {
  const port: ProjectPickerPort = { pick: vi.fn().mockResolvedValue(null), ...picker };
  render(
    <AppProviders>
      <ProjectSelectionProvider picker={port}>
        <MemoryRouter>
          <HomePage client={client} findShortcuts={findShortcuts} />
          <Where />
        </MemoryRouter>
      </ProjectSelectionProvider>
    </AppProviders>
  );
  return { port, client };
}

const hosted = () => {
  (window as unknown as Record<string, unknown>).__TAURI_INTERNALS__ = {};
  invoke.mockResolvedValue('ready');
};

beforeEach(() => {
  window.localStorage.clear();
  invoke.mockReset();
});

afterEach(() => {
  delete (window as unknown as Record<string, unknown>).__TAURI_INTERNALS__;
});

describe('Início · quem ainda não tem projeto', () => {
  it('pergunta o que a pessoa quer fazer e oferece o caminho para começar', () => {
    renderHome();

    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('Olá! O que você deseja realizar hoje?');
    const suggestions = screen.getByRole('region', { name: 'Sugestões' });
    expect(
      within(suggestions)
        .getAllByRole('button')
        .map((button) => button.textContent)
    ).toEqual([
      expect.stringContaining('Conectar meu primeiro projeto'),
      expect.stringContaining('Consultar meus projetos'),
      expect.stringContaining('Conectar ferramentas')
    ]);
  });

  it('conectar o primeiro projeto pede a pasta e só segue com ela', async () => {
    const { port } = renderHome();

    await userEvent.click(screen.getByRole('button', { name: /Conectar meu primeiro projeto/ }));

    expect(port.pick).toHaveBeenCalled();
    expect(screen.getByTestId('where')).toHaveTextContent(/^\/$/);
  });

  it('quem consulta projetos vai ao Portfólio, e quem conecta ferramentas às Conexões, sem escolher pasta', async () => {
    const { port } = renderHome();

    await userEvent.click(screen.getByRole('button', { name: /Consultar meus projetos/ }));
    expect(screen.getByTestId('where')).toHaveTextContent('/portfolio');

    await userEvent.click(screen.getByRole('button', { name: /Conectar ferramentas/ }));
    expect(screen.getByTestId('where')).toHaveTextContent('/integracoes');
    expect(port.pick).not.toHaveBeenCalled();
  });

  it('mostra os primeiros passos, com o que depende de um projeto dizendo isso', () => {
    renderHome();

    const steps = screen.getByRole('region', { name: 'Primeiros passos' });
    expect(within(steps).getByText('0 de 4 concluídos')).toBeInTheDocument();
    expect(within(steps).getAllByRole('listitem')).toHaveLength(4);
    expect(within(steps).getByRole('button', { name: 'Selecionar pasta' })).toBeInTheDocument();
    expect(within(steps).getByText('Depois de abrir um projeto')).toBeInTheDocument();
    expect(within(steps).getByRole('link', { name: 'Conectar modelo' })).toHaveAttribute('href', '/conexoes/modelos');
  });

  it('quem já abriu projetos antes é convidado a abrir um, não a conectar o primeiro', async () => {
    renderHome({ picker: { recents: vi.fn().mockResolvedValue([recent]) } });

    expect(await screen.findByRole('button', { name: /Abrir um projeto/ })).toBeInTheDocument();
    expect(screen.getByRole('region', { name: 'Projetos recentes' })).toBeInTheDocument();
  });
});

describe('Início · com projeto aberto', () => {
  const withProject = { pick: vi.fn().mockResolvedValue(project), recents: vi.fn().mockResolvedValue([recent]) };

  async function openProject() {
    await userEvent.click(screen.getByRole('button', { name: /Abrir um projeto|Conectar meu primeiro projeto/ }));
    await waitFor(() => expect(screen.getByTestId('where')).toHaveTextContent('/configurar'));
  }

  it('as sugestões passam a ser atalhos para continuar o trabalho', async () => {
    renderHome({ picker: withProject });
    await openProject();

    const suggestions = screen.getByRole('region', { name: 'Sugestões' });
    expect(
      within(suggestions)
        .getAllByRole('button')
        .map((button) => button.textContent)
    ).toEqual([
      expect.stringContaining('Analisar o projeto'),
      expect.stringContaining('Configurar meu ambiente'),
      expect.stringContaining('Acompanhar minhas entregas'),
      expect.stringContaining('Da ideia ao projeto'),
      expect.stringContaining('Consultar meus projetos'),
      expect.stringContaining('Conectar ferramentas')
    ]);

    await userEvent.click(within(suggestions).getByRole('button', { name: /Acompanhar minhas entregas/ }));
    expect(screen.getByTestId('where')).toHaveTextContent('/gestao');
  });

  it('os primeiros passos marcam só o que o motor confirma', async () => {
    hosted();
    renderHome({ picker: withProject, client: engine({ model: true, applied: true, connections: 0 }) });
    await openProject();

    const steps = screen.getByRole('region', { name: 'Primeiros passos' });
    expect(await within(steps).findByText('3 de 4 concluídos')).toBeInTheDocument();
    expect(within(steps).getAllByRole('img', { name: 'Concluído' })).toHaveLength(3);
    expect(within(steps).getByRole('link', { name: 'Conectar' })).toHaveAttribute('href', '/integracoes');
  });

  it('com tudo feito, os primeiros passos saem da tela', async () => {
    hosted();
    renderHome({ picker: withProject, client: engine({ model: true, applied: true, connections: 2 }) });
    await openProject();

    await waitFor(() => expect(screen.queryByRole('region', { name: 'Primeiros passos' })).not.toBeInTheDocument());
  });

  it('ocultar os primeiros passos vale para a próxima abertura, e dá para trazê-los de volta', async () => {
    renderHome();

    await userEvent.click(screen.getByRole('button', { name: 'Ocultar' }));
    expect(screen.queryByRole('region', { name: 'Primeiros passos' })).not.toBeInTheDocument();
    expect(window.localStorage.getItem('spock.setup')).toBe('hidden');

    await userEvent.click(screen.getByRole('button', { name: 'Mostrar os primeiros passos' }));
    expect(screen.getByRole('region', { name: 'Primeiros passos' })).toBeInTheDocument();
  });

  it('a atividade recente aparece só quando há o que mostrar, e cada linha abre o detalhe', async () => {
    hosted();
    renderHome({ client: engine({ runs: [run('r-1', 'failed'), run('r-2', 'completed')] }) });

    const activity = await screen.findByRole('region', { name: 'Atividade recente' });
    expect(within(activity).getAllByRole('button')).toHaveLength(2);
    expect(within(activity).getByRole('button', { name: /Validação.*Falhou/ })).toHaveAttribute(
      'aria-haspopup',
      'dialog'
    );
    expect(within(activity).getByRole('link', { name: 'Ver toda a atividade' })).toHaveAttribute('href', '/execucoes');
  });

  it('sem execuções, ou fora do aplicativo, não há bloco de atividade', async () => {
    renderHome({ client: engine({ runs: [run('r-1', 'failed')] }) });

    expect(screen.queryByRole('region', { name: 'Atividade recente' })).not.toBeInTheDocument();
  });
});

describe('Início · entrada de pedidos', () => {
  const field = () => screen.getByRole('textbox', { name: 'O que você deseja fazer?' });

  it('sugere o tipo de atividade pelo texto, e a escolha da pessoa passa a valer', async () => {
    renderHome();
    expect(screen.getByRole('radio', { name: 'Pedido de engenharia' })).toBeChecked();

    await userEvent.type(field(), 'Quais são os riscos do projeto?');
    expect(screen.getByRole('radio', { name: 'Pergunta sobre o andamento' })).toBeChecked();

    await userEvent.click(screen.getByRole('radio', { name: 'Nova ideia' }));
    await userEvent.type(field(), ' e os prazos');
    expect(screen.getByRole('radio', { name: 'Nova ideia' })).toBeChecked();
  });

  it('oferece os quatro caminhos, com nomes que dizem para que servem', () => {
    renderHome();

    expect(screen.getAllByRole('radio').map((radio) => radio.getAttribute('value'))).toEqual([
      'request',
      'agent',
      'assistant',
      'idea'
    ]);
    expect(screen.getByRole('radio', { name: 'Pergunta sobre o código' })).toBeInTheDocument();
  });

  it('uma pergunta sem vocabulário de gestão é sobre o código, e segue para a tela de agentes', async () => {
    renderHome({ picker: { pick: vi.fn().mockResolvedValue(project) } });

    await userEvent.type(field(), 'Como funciona a autenticação?{Enter}');

    await waitFor(() => expect(screen.getByTestId('where')).toHaveTextContent('/agentes'));
    expect(screen.getByTestId('handoff')).toHaveTextContent('Como funciona a autenticação?');
  });

  it('avisa quando o caminho escolhido usa um modelo de IA e nenhum está conectado, e diz onde conectar', async () => {
    hosted();
    renderHome({ client: engine({ model: false, agent: true }) });

    await userEvent.type(field(), 'Quais são os riscos do projeto?');

    const notice = await screen.findByRole('note');
    expect(notice).toHaveTextContent(/usa um modelo de IA.*nenhum está conectado/);
    expect(within(notice).getByRole('link', { name: 'Conectar um modelo de IA' })).toHaveAttribute(
      'href',
      '/conexoes/modelos'
    );
    expect(field()).toHaveAccessibleDescription(/nenhum está conectado/);
  });

  it('avisa quando a pergunta sobre o código não tem ferramenta de IA instalada para responder', async () => {
    hosted();
    renderHome({ client: engine({ model: true, agent: false }) });

    await userEvent.click(screen.getByRole('radio', { name: 'Pergunta sobre o código' }));

    expect(await screen.findByRole('note')).toHaveTextContent(/nenhuma foi encontrada/);
  });

  it('não avisa nada para o caminho que não depende do que falta', async () => {
    hosted();
    const { client } = renderHome({ client: engine({ model: false, agent: false }) });
    await userEvent.type(field(), 'trocar a cor do botão');
    // O motor já respondeu que não há modelo nem ferramenta: o pedido de engenharia roda sem os dois.
    await waitFor(() => expect(client.request).toHaveBeenCalledWith('aitool.list', {}, expect.anything()));
    await waitFor(() => expect(client.request).toHaveBeenCalledWith('model.status', {}, expect.anything()));

    expect(screen.getByRole('radio', { name: 'Pedido de engenharia' })).toBeChecked();
    expect(screen.queryByRole('note')).not.toBeInTheDocument();

    // Com a mesma resposta do motor, trocar para um caminho que depende do modelo faz o aviso aparecer.
    await userEvent.click(screen.getByRole('radio', { name: 'Pergunta sobre o andamento' }));
    expect(await screen.findByRole('note')).toBeInTheDocument();
  });

  it('fora do aplicativo não há motor para perguntar, então não há aviso', async () => {
    renderHome();

    await userEvent.type(field(), 'Quais são os riscos do projeto?');

    expect(screen.queryByRole('note')).not.toBeInTheDocument();
  });

  it('só continua com texto, e explica o que vai acontecer', async () => {
    renderHome();

    expect(screen.getByRole('button', { name: 'Continuar' })).toBeDisabled();
    expect(field()).toHaveAccessibleDescription(/Analisa o pedido no projeto e mostra o impacto/);
    expect(field()).toHaveAccessibleDescription(/Ao continuar, você escolhe a pasta do projeto/);
  });

  it('Enter leva o texto à tela que trata o pedido, depois de escolher a pasta', async () => {
    const { port } = renderHome({ picker: { pick: vi.fn().mockResolvedValue(project) } });

    await userEvent.type(field(), 'trocar o contrato de autenticação{Enter}');

    expect(port.pick).toHaveBeenCalled();
    await waitFor(() => expect(screen.getByTestId('where')).toHaveTextContent('/pedido'));
    expect(screen.getByTestId('handoff')).toHaveTextContent('trocar o contrato de autenticação');
  });

  it('Shift+Enter quebra a linha em vez de continuar', async () => {
    renderHome();

    await userEvent.type(field(), 'primeira linha{Shift>}{Enter}{/Shift}segunda');

    expect(field()).toHaveValue('primeira linha\nsegunda');
    expect(screen.getByTestId('where')).toHaveTextContent(/^\/$/);
  });

  it('quem desiste de escolher a pasta fica onde estava, com o texto', async () => {
    renderHome();

    await userEvent.type(field(), 'trocar o contrato de autenticação');
    await userEvent.click(screen.getByRole('button', { name: 'Continuar' }));

    expect(screen.getByTestId('where')).toHaveTextContent(/^\/$/);
    expect(field()).toHaveValue('trocar o contrato de autenticação');
  });

  it('oferece as telas relacionadas ao texto, para quem procurava um lugar', async () => {
    const findShortcuts = vi.fn(() => [{ id: 's', label: 'Serviços', hint: 'Conexões', to: '/integracoes' }]);
    renderHome({ findShortcuts });

    await userEvent.type(field(), 'configure as integrações');

    const shortcuts = screen.getByRole('navigation', { name: 'Telas relacionadas ao que você escreveu' });
    expect(within(shortcuts).getByRole('link', { name: /Serviços/ })).toHaveAttribute('href', '/integracoes');
    expect(findShortcuts).toHaveBeenLastCalledWith('configure as integrações');
  });

  it('não tem violações estruturais de acessibilidade', async () => {
    renderHome();
    await userEvent.type(field(), 'analise meu projeto');

    await expectNoA11yViolations(document.body);
  });
});

describe('Início · o pedido passa pelo Boot', () => {
  const field = () => screen.getByRole('textbox', { name: 'O que você deseja fazer?' });
  const withProject = { pick: vi.fn().mockResolvedValue(project), recents: vi.fn().mockResolvedValue([recent]) };

  const bootCalls = (client: EngineClient) =>
    vi
      .mocked(client.request)
      .mock.calls.filter(([method]) => String(method).startsWith('boot.') && method !== 'boot.list');

  it('uma pergunta de leitura mostra o que o Spock consultou, sem sair da tela e sem alterar nada', async () => {
    hosted();
    const client = engine({ boot: () => bootAnswered });
    renderHome({ picker: withProject, client });

    await userEvent.type(field(), 'Quais tecnologias o projeto usa?{Enter}');

    const card = await screen.findByRole('region', { name: 'Resultado do preparo do pedido' });
    expect(within(card).getByText('Consultado')).toBeInTheDocument();
    expect(within(card).getByText('Nenhuma alteração foi feita no projeto.')).toBeInTheDocument();
    expect(screen.getByTestId('where')).toHaveTextContent(/^\/$/);
    const [method, params] = bootCalls(client)[0]!;
    expect(method).toBe('boot.start');
    expect(params).toMatchObject({
      projectId: 'p-1',
      text: 'Quais tecnologias o projeto usa?',
      hint: 'agent',
      source: 'home'
    });
    expect(params).not.toHaveProperty('capabilityId');
  });

  it('abrir a tela sugerida leva o texto junto, como antes', async () => {
    hosted();
    renderHome({ picker: withProject, client: engine({ boot: () => bootHandoff }) });

    await userEvent.type(field(), 'Configurar meu ambiente{Enter}');
    const card = await screen.findByRole('region', { name: 'Resultado do preparo do pedido' });
    await userEvent.click(within(card).getByRole('button', { name: /Configurar|Abrir/ }));

    expect(screen.getByTestId('where')).toHaveTextContent('/configurar');
    expect(screen.getByTestId('handoff')).toHaveTextContent('Configurar meu ambiente');
  });

  it('o tipo marcado de propósito vai como capacidade, sem interpretar o texto', async () => {
    hosted();
    const client = engine({ boot: () => bootAnswered });
    renderHome({ picker: withProject, client });

    await userEvent.click(screen.getByRole('radio', { name: 'Nova ideia' }));
    await userEvent.type(field(), 'um aplicativo de entregas{Enter}');

    await screen.findByRole('region', { name: 'Resultado do preparo do pedido' });
    expect(bootCalls(client)[0]![1]).toMatchObject({ capabilityId: 'ideas.idea_to_project' });
    expect(bootCalls(client)[0]![1]).not.toHaveProperty('hint');
  });

  it('pedido ambíguo vira pergunta, e escolher uma opção refaz o pedido com a capacidade', async () => {
    hosted();
    const client = engine({ boot: (_method, params) => (params.capabilityId ? bootAnswered : bootClarify) });
    renderHome({ picker: withProject, client });

    await userEvent.type(field(), 'Quero ver os riscos e as entregas{Enter}');
    const card = await screen.findByRole('region', { name: 'Resultado do preparo do pedido' });
    expect(within(card).getByText('Preciso de uma escolha')).toBeInTheDocument();
    const options = within(card)
      .getAllByRole('button')
      .filter((button) => !/Fechar|Abrir/.test(button.textContent ?? ''));
    expect(options.length).toBeGreaterThanOrEqual(2);

    await userEvent.click(options[0]!);

    await waitFor(() => expect(bootCalls(client)).toHaveLength(2));
    expect(bootCalls(client)[1]![1]).toMatchObject({
      projectId: 'p-1',
      text: 'Quero ver os riscos e as entregas',
      capabilityId: expect.any(String)
    });
  });

  it('se o motor não responde ao Boot, o pedido segue para a tela como sempre seguiu', async () => {
    hosted();
    renderHome({
      picker: withProject,
      client: engine({
        boot: () => {
          throw new Error('motor fora');
        }
      })
    });

    await userEvent.type(field(), 'Como funciona a autenticação?{Enter}');

    await waitFor(() => expect(screen.getByTestId('where')).toHaveTextContent('/agentes'));
    expect(screen.getByTestId('handoff')).toHaveTextContent('Como funciona a autenticação?');
  });

  it('"Analisar o projeto" roda o Project Boot e só abre a tela quando a pessoa pede', async () => {
    hosted();
    const client = engine({ boot: () => bootProject });
    renderHome({ picker: withProject, client });
    await userEvent.click(screen.getByRole('button', { name: /Abrir um projeto|Conectar meu primeiro projeto/ }));
    await waitFor(() => expect(screen.getByTestId('where')).toHaveTextContent('/configurar'));
    await userEvent.click(screen.getByRole('button', { name: /Analisar o projeto/ }));

    const card = await screen.findByRole('region', { name: 'Resultado do preparo do pedido' });
    expect(within(card).getByText('Preparado')).toBeInTheDocument();
    expect(bootCalls(client)[0]).toEqual(['boot.project', { projectId: 'p-1', source: 'home' }, expect.anything()]);
  });

  it('com um pedido em preparo o Continuar espera: um segundo Enter não cria outro Boot', async () => {
    hosted();
    const base = engine({ boot: () => bootAnswered });
    let release: () => void = () => undefined;
    const gate = new Promise<void>((resolve) => {
      release = resolve;
    });
    const client = {
      ...base,
      request: vi.fn(async (method: string, params: Record<string, unknown>, schema: never) => {
        if (method === 'boot.start') await gate;
        return base.request(method as never, params, schema);
      })
    } as unknown as EngineClient;
    renderHome({ picker: withProject, client });

    await userEvent.type(field(), 'Quais tecnologias o projeto usa?{Enter}');
    await screen.findByRole('status');
    expect(screen.getByRole('button', { name: 'Continuar' })).toBeDisabled();
    await userEvent.keyboard('{Enter}');
    release();

    await screen.findByRole('region', { name: 'Resultado do preparo do pedido' });
    expect(bootCalls(client)).toHaveLength(1);
  });

  it('se a escolha de uma opção falha, a tela diz que não deu certo em vez de sumir com o cartão', async () => {
    hosted();
    const client = engine({
      boot: (_method, params) => {
        if (params.capabilityId) throw new Error('motor fora');
        return bootClarify;
      }
    });
    renderHome({ picker: withProject, client });

    await userEvent.type(field(), 'Quero ver os riscos e as entregas{Enter}');
    const card = await screen.findByRole('region', { name: 'Resultado do preparo do pedido' });
    const option = within(card)
      .getAllByRole('button')
      .find((button) => !/Fechar|Abrir/.test(button.textContent ?? ''))!;
    await userEvent.click(option);

    expect(await screen.findByText(/motor fora|não conseguiu preparar/i)).toBeInTheDocument();
    expect(screen.queryByRole('region', { name: 'Resultado do preparo do pedido' })).not.toBeInTheDocument();
  });

  it('o resultado do Boot não tem violações estruturais de acessibilidade', async () => {
    hosted();
    renderHome({ picker: withProject, client: engine({ boot: () => bootProject }) });
    await userEvent.type(field(), 'Quais tecnologias o projeto usa?{Enter}');
    await screen.findByRole('region', { name: 'Resultado do preparo do pedido' });

    await expectNoA11yViolations(document.body);
  });
});

describe('Início · pedidos recentes e Retomar', () => {
  const withProject = { pick: vi.fn().mockResolvedValue(project), recents: vi.fn().mockResolvedValue([recent]) };
  async function openProject() {
    await userEvent.click(screen.getByRole('button', { name: /Abrir um projeto|Conectar meu primeiro projeto/ }));
    await waitFor(() => expect(screen.getByTestId('where')).toHaveTextContent('/configurar'));
  }
  const region = () => screen.findByRole('region', { name: 'Pedidos recentes' });
  const card = () => screen.findByRole('region', { name: 'Resultado do preparo do pedido' });

  /** A lista do contrato com os três tipos de linha: o que a queda deixou no meio, o que já foi recuperado e o concluído. */
  const mixedList = () => {
    const [clarify, handoff, answered, prepared] = bootList.boots;
    return bootListSchema.parse({
      boots: [
        { ...clarify, state: 'interrupted', outcome: null, headline: 'Revisar os riscos do projeto', resumable: true },
        {
          ...handoff,
          state: 'interrupted',
          outcome: null,
          headline: 'Configurar o ambiente',
          recoveredByBootId: 'boot-00000000000a'
        },
        answered,
        { ...prepared, mode: 'recovery', recoversBootId: 'boot-000000000001' }
      ]
    });
  };

  const resumed = { boot: { ...bootAnswered.boot, mode: 'recovery', recoversBootId: 'boot-000000000001' } };
  const calls = (client: EngineClient, method: string) =>
    vi.mocked(client.request).mock.calls.filter(([called]) => called === method);
  const bootActions = (client: EngineClient) =>
    vi
      .mocked(client.request)
      .mock.calls.filter(([method]) => String(method).startsWith('boot.') && method !== 'boot.list');

  it('lista os pedidos do projeto com o resultado, o horário e o aviso de recuperação', async () => {
    hosted();
    const client = engine({ bootList: mixedList });
    renderHome({ picker: withProject, client });
    await openProject();

    const section = await region();
    const rows = within(within(section).getByRole('list', { name: 'Últimos pedidos do projeto' })).getAllByRole(
      'listitem'
    );
    expect(rows).toHaveLength(4);
    expect(calls(client, 'boot.list')[0]![1]).toEqual({ projectId: 'p-1' });
    expect(within(rows[0]!).getByText('Revisar os riscos do projeto')).toBeInTheDocument();
    expect(within(rows[0]!).getByText('Interrompido')).toBeInTheDocument();
    expect(within(rows[0]!).getByText(/\d{2}:\d{2}/)).toBeInTheDocument();
    expect(within(rows[1]!).getByText(/recuperado/)).toBeInTheDocument();
    expect(within(rows[2]!).getByText('Consultado')).toBeInTheDocument();
    expect(within(rows[3]!).getByText(/recupera outro/)).toBeInTheDocument();
    expect(within(rows[3]!).getByText('Preparado')).toBeInTheDocument();
  });

  it('o interrompido sem manchete mostra o pedido, e sem plano não oferece Retomar', async () => {
    hosted();
    const [base] = bootList.boots;
    const list = () =>
      bootListSchema.parse({
        boots: [
          {
            ...base,
            id: 'boot-00000000000b',
            state: 'interrupted',
            outcome: null,
            headline: null,
            objective: 'Quais riscos o projeto tem?',
            resumable: true
          },
          {
            ...base,
            id: 'boot-00000000000c',
            state: 'interrupted',
            outcome: null,
            headline: null,
            objective: 'Corrigir o bug do frete',
            resumable: false
          }
        ]
      });
    renderHome({ picker: withProject, client: engine({ bootList: list }) });
    await openProject();

    const section = await region();
    expect(within(section).getByText('Quais riscos o projeto tem?')).toBeInTheDocument();
    expect(within(section).getByText('Corrigir o bug do frete')).toBeInTheDocument();
    expect(within(section).getAllByRole('button', { name: /^Retomar/ })).toHaveLength(1);
    expect(within(section).getByRole('button', { name: 'Retomar: Quais riscos o projeto tem?' })).toBeInTheDocument();
  });

  it('Retomar só aparece no interrompido que ainda não foi recuperado', async () => {
    hosted();
    renderHome({ picker: withProject, client: engine({ bootList: mixedList }) });
    await openProject();

    const section = await region();
    const buttons = within(section).getAllByRole('button', { name: /^Retomar/ });
    expect(buttons).toHaveLength(1);
    expect(buttons[0]).toHaveAccessibleName('Retomar: Revisar os riscos do projeto');
  });

  it('Retomar pede ao motor o projeto e o Boot certos e mostra o resultado no mesmo cartão', async () => {
    hosted();
    let list = mixedList();
    const client = engine({
      bootList: () => list,
      boot: (method) => {
        if (method !== 'boot.resume') throw new Error(`inesperado: ${method}`);
        list = bootListSchema.parse({
          boots: [
            { ...list.boots[0], recoveredByBootId: 'boot-0000000000bb', resumable: false },
            ...list.boots.slice(1)
          ]
        });
        return resumed;
      }
    });
    renderHome({ picker: withProject, client });
    await openProject();

    await userEvent.click(await screen.findByRole('button', { name: 'Retomar: Revisar os riscos do projeto' }));

    expect(within(await card()).getByText('Consultado')).toBeInTheDocument();
    expect(bootActions(client)).toEqual([
      ['boot.resume', { projectId: 'p-1', bootId: 'boot-000000000001' }, expect.anything()]
    ]);
    await waitFor(() => expect(calls(client, 'boot.list')).toHaveLength(2));
    await waitFor(() => expect(screen.queryByRole('button', { name: /^Retomar/ })).not.toBeInTheDocument());
    expect(await region()).toBeInTheDocument();
  });

  it('uma linha concluída abre o detalhe no mesmo cartão', async () => {
    hosted();
    const client = engine({ bootList: mixedList, boot: () => bootAnswered });
    renderHome({ picker: withProject, client });
    await openProject();

    await userEvent.click(await screen.findByRole('button', { name: /^Abrir: 3 tecnologias no projeto\./ }));

    expect(within(await card()).getByText('Consultado')).toBeInTheDocument();
    expect(bootActions(client)).toEqual([
      ['boot.detail', { projectId: 'p-1', bootId: 'boot-000000000003' }, expect.anything()]
    ]);
  });

  it('com um Boot em preparo o Retomar espera e um segundo clique não cria outro', async () => {
    hosted();
    const base = engine({ bootList: mixedList, boot: () => resumed });
    let release: () => void = () => undefined;
    const gate = new Promise<void>((resolve) => {
      release = resolve;
    });
    const client = {
      ...base,
      request: vi.fn(async (method: string, params: Record<string, unknown>, schema: never) => {
        if (method === 'boot.resume') await gate;
        return base.request(method as never, params, schema);
      })
    } as unknown as EngineClient;
    renderHome({ picker: withProject, client });
    await openProject();

    await userEvent.click(await screen.findByRole('button', { name: /^Retomar/ }));
    await screen.findByText('Retomando o pedido…');
    expect(screen.getByRole('button', { name: /^Retomar/ })).toBeDisabled();
    fireEvent.click(screen.getByRole('button', { name: /^Retomar/ }));
    release();

    await card();
    expect(bootActions(client)).toHaveLength(1);
  });

  it('se o motor falha ao retomar, avisa e o bloco continua na tela', async () => {
    hosted();
    const client = engine({
      bootList: mixedList,
      boot: () => {
        throw new Error('motor fora');
      }
    });
    renderHome({ picker: withProject, client });
    await openProject();

    await userEvent.click(await screen.findByRole('button', { name: /^Retomar/ }));

    expect(await screen.findByText('O Spock não conseguiu retomar este pedido.')).toBeInTheDocument();
    expect(await region()).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /^Retomar/ })).toBeEnabled();
  });

  it('se a lista não carrega, avisa e deixa tentar de novo', async () => {
    hosted();
    let broken = true;
    const client = engine({
      bootList: () => {
        if (broken) throw new Error('motor fora');
        return mixedList();
      }
    });
    renderHome({ picker: withProject, client });
    await openProject();

    const section = await region();
    expect(await within(section).findByRole('alert')).toHaveTextContent(
      'O Spock não conseguiu buscar os últimos pedidos.'
    );
    broken = false;
    await userEvent.click(within(section).getByRole('button', { name: 'Tentar de novo' }));

    expect(await screen.findByRole('list', { name: 'Últimos pedidos do projeto' })).toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('sem nenhum Boot o bloco não aparece', async () => {
    hosted();
    const client = engine({});
    renderHome({ picker: withProject, client });
    await openProject();

    await waitFor(() => expect(calls(client, 'boot.list')).toHaveLength(1));
    await waitFor(() => expect(screen.queryByRole('region', { name: 'Pedidos recentes' })).not.toBeInTheDocument());
  });

  it('sem projeto selecionado não pergunta nada ao motor e o bloco não aparece', async () => {
    hosted();
    const client = engine({ bootList: mixedList });
    renderHome({ client });
    await screen.findByRole('heading', { level: 1 });

    expect(calls(client, 'boot.list')).toHaveLength(0);
    expect(screen.queryByRole('region', { name: 'Pedidos recentes' })).not.toBeInTheDocument();
  });

  it('fora do aplicativo (sem o motor) não pergunta nada ao motor', async () => {
    const client = engine({ bootList: mixedList });
    renderHome({ picker: withProject, client });
    await openProject();
    await screen.findByRole('heading', { level: 1 });

    expect(calls(client, 'boot.list')).toHaveLength(0);
    expect(screen.queryByRole('region', { name: 'Pedidos recentes' })).not.toBeInTheDocument();
  });

  it('a lista de pedidos recentes não tem violações estruturais de acessibilidade', async () => {
    hosted();
    renderHome({ picker: withProject, client: engine({ bootList: mixedList }) });
    await openProject();
    await region();

    await expectNoA11yViolations(document.body);
  });
});
