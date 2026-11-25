import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, useLocation } from 'react-router';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { ProjectSelectionProvider, type ProjectPickerPort } from '@/features/project-selection';
import type { EngineClient } from '@/shared/api';
import { AppProviders } from '@/shared/components';
import { readHandoff } from '@/shared/routing';
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
