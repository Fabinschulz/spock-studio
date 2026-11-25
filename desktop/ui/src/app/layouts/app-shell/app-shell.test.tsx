import { act, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { ReactNode } from 'react';
import { MemoryRouter, useLocation } from 'react-router';
import timelineFixture from '@contracts/fixtures/timeline/timeline.json';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { ProjectSelectionProvider, useProjectSelectionContext } from '@/features/project-selection';
import { AppProviders, Page } from '@/shared/components';
import { expectNoA11yViolations } from '@/test/a11y';

import { AppShell } from './AppShell';
import { NavEntry } from './NavEntry';

const invoke = vi.fn();
const listen = vi.fn();
vi.mock('@tauri-apps/api/core', () => ({ invoke: (...args: unknown[]) => invoke(...args) }));
vi.mock('@tauri-apps/api/event', () => ({ listen: (...args: unknown[]) => listen(...args) }));

const project = { projectId: 'p-1', displayName: 'EmpregaNetAPI', displayPath: '~\\EmpregaNetAPI' };

function Pick() {
  const { pick } = useProjectSelectionContext();
  return (
    <button type="button" onClick={() => void pick()}>
      escolher
    </button>
  );
}

function Where() {
  return <p data-testid="where">{useLocation().pathname}</p>;
}

function renderShell(route = '/', page?: ReactNode) {
  return render(
    <AppProviders>
      <ProjectSelectionProvider picker={{ pick: vi.fn().mockResolvedValue(project) }}>
        <MemoryRouter initialEntries={[route]}>
          <AppShell>
            <Pick />
            <Where />
            <h1>Conteúdo</h1>
            {page}
          </AppShell>
        </MemoryRouter>
      </ProjectSelectionProvider>
    </AppProviders>
  );
}

const areas = () => screen.getByRole('navigation', { name: 'Navegação principal' });
const areaPanel = (name: string) => screen.getByRole('navigation', { name });
const queryAreaPanel = (name: string) => screen.queryByRole('navigation', { name });

/** O jsdom não tem `matchMedia`: aqui a janela "casa" só com as consultas pedidas. */
function narrowWindow(...queries: string[]) {
  window.matchMedia = ((query: string) => ({ matches: queries.includes(query) })) as typeof window.matchMedia;
}
const NARROW = '(max-width: 863.98px)';
const COMPACT = '(max-width: 599.98px)';

beforeEach(() => {
  window.localStorage.clear();
  invoke.mockReset();
  listen.mockReset().mockResolvedValue(() => undefined);
});

afterEach(() => {
  delete (window as unknown as Record<string, unknown>).__TAURI_INTERNALS__;
  delete (window as unknown as Record<string, unknown>).matchMedia;
});

describe('AppShell · áreas', () => {
  it('a marca leva ao início e tem nome acessível, e o conteúdo fica no <main>', () => {
    renderShell();

    expect(screen.getByRole('link', { name: 'Spock Studio, página inicial' })).toHaveAttribute('href', '/');
    expect(within(screen.getByRole('main')).getByRole('heading', { name: 'Conteúdo' })).toBeInTheDocument();
  });

  it('a barra mostra as áreas, cada uma com o nome à vista, e marca a atual', () => {
    renderShell();
    const nav = areas();

    expect(
      within(nav)
        .getAllByRole('link')
        .map((link) => link.textContent)
    ).toEqual(['Início', 'Engenharia', 'Gestão', 'Atividade', 'Conexões', 'Configurações']);
    expect(within(nav).getByRole('link', { name: 'Início' })).toHaveAttribute('aria-current', 'page');
    expect(within(nav).getByRole('link', { name: 'Engenharia' })).toHaveAttribute('href', '/projeto');
    expect(within(nav).getByRole('link', { name: 'Atividade' })).toHaveAttribute('href', '/execucoes');
    expect(within(nav).getByRole('link', { name: 'Conexões' })).toHaveAttribute('href', '/integracoes');
    expect(within(nav).getByRole('link', { name: 'Configurações' })).toHaveAttribute('href', '/configuracoes/ia');
  });

  it('no início não há painel de área: a tela é toda da entrada de pedidos', () => {
    renderShell();

    expect(screen.getAllByRole('navigation')).toHaveLength(1);
    expect(screen.queryByRole('button', { name: 'Recolher menu' })).not.toBeInTheDocument();
  });

  it('dentro de uma área, o painel ao lado lista as telas dela e marca a atual', () => {
    renderShell('/projeto');
    const panel = areaPanel('Engenharia');

    expect(
      within(panel)
        .getAllByRole('link')
        .map((link) => link.textContent)
    ).toEqual([
      'Visão geral',
      'Padrões do projeto',
      'Configurar',
      'Pedidos',
      'Conhecimento',
      'Segurança',
      'Infraestrutura'
    ]);
    expect(within(panel).getByRole('link', { name: 'Visão geral' })).toHaveAttribute('aria-current', 'page');
    expect(within(panel).getByRole('link', { name: 'Padrões do projeto' })).toHaveAttribute('href', '/projeto/harness');
    expect(within(panel).getByRole('link', { name: 'Configurar' })).toHaveAttribute('href', '/configurar');
    // Na barra, a área fica marcada como a atual também nas telas que não são a inicial dela.
    expect(within(areas()).getByRole('link', { name: 'Engenharia' })).toHaveAttribute('aria-current', 'page');
  });

  it('numa tela interna a área segue marcada, como conjunto, e a Visão geral não', () => {
    renderShell('/projeto/harness');

    expect(within(areas()).getByRole('link', { name: 'Engenharia' })).toHaveAttribute('aria-current', 'true');
    const panel = areaPanel('Engenharia');
    expect(within(panel).getByRole('link', { name: 'Padrões do projeto' })).toHaveAttribute('aria-current', 'page');
    expect(within(panel).getByRole('link', { name: 'Visão geral' })).not.toHaveAttribute('aria-current');
  });

  it('Gestão, Atividade, Conexões e Configurações têm cada uma o seu painel', () => {
    const cases: [string, string, string[]][] = [
      ['/gestao', 'Gestão', ['Portfólio', 'Acompanhamento', 'Assistente', 'Relatório']],
      ['/linha-do-tempo', 'Atividade', ['Execuções', 'Histórico']],
      ['/cloud', 'Conexões', ['Serviços', 'Nuvem', 'Modelos de IA']],
      ['/configuracoes/diagnostico', 'Configurações', ['Minha IA', 'Diagnóstico', 'Sobre']]
    ];
    for (const [route, name, items] of cases) {
      const { unmount } = renderShell(route);
      expect(
        within(areaPanel(name))
          .getAllByRole('link')
          .map((link) => link.textContent)
      ).toEqual(items);
      expect(within(areas()).getByRole('link', { name })).toHaveAttribute('aria-current');
      unmount();
    }
  });

  it('um item com abas fica marcado em qualquer uma delas, e a página mostra as abas', () => {
    renderShell('/projeto/catalogo', <Page title="Catálogo" />);

    expect(within(areaPanel('Engenharia')).getByRole('link', { name: 'Padrões do projeto' })).toHaveAttribute(
      'aria-current',
      'page'
    );
    const tabs = screen.getByRole('navigation', { name: 'Seções de Padrões do projeto' });
    expect(within(tabs).getByRole('link', { name: 'Catálogo' })).toHaveAttribute('aria-current', 'page');
    expect(within(tabs).getByRole('link', { name: 'Estado e revisões' })).toHaveAttribute('href', '/projeto/harness');
  });

  it('a tela que não divide item com outra não mostra abas', () => {
    renderShell('/projeto/conhecimento', <Page title="Conhecimento" />);

    expect(screen.queryByRole('navigation', { name: /^Seções de/ })).not.toBeInTheDocument();
  });

  it('nenhum item à vista repete o ícone de outro', () => {
    renderShell('/projeto');

    const icons = [
      ...within(areas()).getAllByRole('link'),
      ...within(areaPanel('Engenharia')).getAllByRole('link')
    ].map((link) => link.querySelector('svg')?.innerHTML);
    expect(icons.every(Boolean)).toBe(true);
    expect(new Set(icons).size).toBe(icons.length);
  });

  it('sem projeto aberto, a Gestão abre no Portfólio; com projeto, no Acompanhamento dele', async () => {
    renderShell();
    // Sem projeto, o Acompanhamento ficaria vazio.
    expect(within(areas()).getByRole('link', { name: 'Gestão' })).toHaveAttribute('href', '/portfolio');

    await userEvent.click(screen.getByRole('button', { name: 'escolher' }));
    await within(screen.getByRole('banner')).findByRole('button', { name: 'Projeto: EmpregaNetAPI' });

    expect(within(areas()).getByRole('link', { name: 'Gestão' })).toHaveAttribute('href', '/gestao');
  });

  it('trocar de área leva à tela dela, troca o painel e não troca o projeto', async () => {
    renderShell('/projeto');
    await userEvent.click(screen.getByRole('button', { name: 'escolher' }));
    const header = screen.getByRole('banner');
    await within(header).findByRole('button', { name: 'Projeto: EmpregaNetAPI' });

    await userEvent.click(within(areas()).getByRole('link', { name: 'Gestão' }));

    expect(screen.getByTestId('where')).toHaveTextContent('/gestao');
    expect(areaPanel('Gestão')).toBeInTheDocument();
    expect(queryAreaPanel('Engenharia')).not.toBeInTheDocument();
    expect(within(header).getByRole('button', { name: 'Projeto: EmpregaNetAPI' })).toBeInTheDocument();
  });

  it('trocar de tela volta o conteúdo ao topo e leva o foco para ele; ao abrir, não mexe no foco', async () => {
    renderShell('/projeto');
    const main = screen.getByRole('main');
    main.scrollTo = vi.fn();
    expect(main).not.toHaveFocus();

    await userEvent.click(within(areaPanel('Engenharia')).getByRole('link', { name: 'Padrões do projeto' }));

    expect(screen.getByTestId('where')).toHaveTextContent('/projeto/harness');
    expect(main.scrollTo).toHaveBeenCalledWith({ top: 0 });
    expect(main).toHaveFocus();
  });

  it('o atalho "Ir para o conteúdo" move o foco para a área principal, sem mudar a rota', async () => {
    renderShell();

    await userEvent.click(screen.getByRole('button', { name: 'Ir para o conteúdo' }));

    expect(screen.getByRole('main')).toHaveFocus();
  });

  it('a navegação por teclado segue a ordem do documento: atalho, marca, áreas', async () => {
    renderShell();

    await userEvent.tab();
    expect(screen.getByRole('button', { name: 'Ir para o conteúdo' })).toHaveFocus();
    await userEvent.tab();
    expect(screen.getByRole('link', { name: /Spock Studio/ })).toHaveFocus();
    await userEvent.tab();
    expect(screen.getByRole('link', { name: 'Início' })).toHaveFocus();
  });

  it('não tem violações estruturais de acessibilidade, no início e dentro de uma área', async () => {
    const home = renderShell();
    await expectNoA11yViolations(home.container);
    home.unmount();

    const { container } = renderShell('/projeto');
    await expectNoA11yViolations(container);
  });
});

describe('AppShell · busca', () => {
  it('o botão do topo abre a paleta, que se fecha com Esc', async () => {
    renderShell();

    await userEvent.click(within(screen.getByRole('banner')).getByRole('button', { name: /Buscar ou pedir algo/ }));
    expect(screen.getByRole('combobox', { name: 'Tela, projeto, comando ou pedido' })).toBeInTheDocument();

    await userEvent.keyboard('{Escape}');
    await waitFor(() => expect(screen.queryByRole('combobox')).not.toBeInTheDocument());
  });

  it('Ctrl+K abre e fecha a paleta de qualquer tela', async () => {
    renderShell('/gestao');

    await userEvent.keyboard('{Control>}k{/Control}');
    expect(screen.getByRole('combobox')).toBeInTheDocument();

    await userEvent.keyboard('{Control>}k{/Control}');
    await waitFor(() => expect(screen.queryByRole('combobox')).not.toBeInTheDocument());
  });

  it('escolher uma tela leva a ela', async () => {
    renderShell();

    await userEvent.keyboard('{Control>}k{/Control}');
    await userEvent.type(screen.getByRole('combobox'), 'relatorio{Enter}');

    expect(screen.getByTestId('where')).toHaveTextContent('/gestao/relatorio');
  });

  it('um comando da paleta age sobre o próprio shell, como mostrar os atalhos', async () => {
    renderShell();

    await userEvent.keyboard('{Control>}k{/Control}');
    await userEvent.type(screen.getByRole('combobox'), 'atalhos de teclado{Enter}');

    expect(await screen.findByRole('dialog', { name: 'Atalhos de teclado' })).toBeInTheDocument();
  });
});

describe('AppShell · trilha', () => {
  it('a página mostra onde está: a área, que leva à tela dela, e o nome da tela', () => {
    renderShell('/projeto/conhecimento', <Page title="Conhecimento" />);

    const trail = within(screen.getByRole('main')).getByRole('navigation', { name: 'Trilha de navegação' });
    expect(within(trail).getByRole('link', { name: 'Engenharia' })).toHaveAttribute('href', '/projeto');
    expect(within(trail).getByText('Conhecimento')).toBeInTheDocument();
  });

  it('o início não tem trilha', () => {
    renderShell('/', <Page title="Início" />);

    expect(screen.queryByRole('navigation', { name: 'Trilha de navegação' })).not.toBeInTheDocument();
  });
});

describe('AppShell · topo', () => {
  it('mostra o projeto aberto e muda quando a pessoa escolhe outra pasta', async () => {
    renderShell();
    const header = screen.getByRole('banner');

    expect(within(header).getByRole('button', { name: /Nenhum projeto aberto/ })).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'escolher' }));

    expect(await within(header).findByRole('button', { name: 'Projeto: EmpregaNetAPI' })).toBeInTheDocument();
  });

  it('o estado do motor aparece uma vez só, na barra de status, junto da versão do pacote', () => {
    renderShell('/projeto');

    const statusbar = screen.getByRole('contentinfo');
    expect(within(statusbar).getByRole('status')).toHaveTextContent('Prévia no navegador');
    expect(within(statusbar).getByText(/^v\d+\.\d+\.\d+/)).toBeInTheDocument();
    expect(screen.getAllByRole('status')).toHaveLength(1);
    const header = screen.getByRole('banner');
    expect(within(header).queryByText(/^v\d+\.\d+\.\d+/)).not.toBeInTheDocument();
    // As Configurações são uma área da barra, não um link perdido na barra de status.
    expect(within(statusbar).queryByRole('link', { name: 'Configurações' })).not.toBeInTheDocument();
  });
});

describe('AppShell · estado do motor', () => {
  it('fora do aplicativo desktop não finge que há motor', () => {
    renderShell();

    const status = screen.getByRole('status');
    expect(status).toHaveTextContent('Prévia no navegador');
    expect(status).toHaveTextContent('Sem motor local');
  });

  it('no aplicativo, mostra o estado que o host informa', async () => {
    (window as unknown as Record<string, unknown>).__TAURI_INTERNALS__ = {};
    invoke.mockResolvedValue('ready');
    renderShell();

    await waitFor(() => expect(screen.getByRole('status')).toHaveTextContent('Pronto para usar'));
    expect(invoke).toHaveBeenCalledWith('engine_status');
  });

  it('acompanha os eventos do motor, incluindo a falha', async () => {
    (window as unknown as Record<string, unknown>).__TAURI_INTERNALS__ = {};
    invoke.mockResolvedValue('starting');
    renderShell();
    await waitFor(() => expect(screen.getByRole('status')).toHaveTextContent('Iniciando o motor'));

    const handler = listen.mock.calls[0]![1] as (event: { payload: unknown }) => void;
    act(() => handler({ payload: 'crashed' }));

    expect(screen.getByRole('status')).toHaveTextContent('Motor indisponível');
  });

  it('inteligência indisponível não aparece como motor caído', async () => {
    (window as unknown as Record<string, unknown>).__TAURI_INTERNALS__ = {};
    invoke.mockResolvedValue({ engine: 'ready', aiRuntime: 'unavailable' });
    renderShell();

    const status = await screen.findByRole('status');
    expect(status).toHaveTextContent('Motor pronto. A inteligência local não está disponível.');
    expect(status).not.toHaveTextContent('Motor indisponível');
  });

  it('um valor desconhecido do host nunca vira "pronto"', async () => {
    (window as unknown as Record<string, unknown>).__TAURI_INTERNALS__ = {};
    invoke.mockResolvedValue('qualquer-coisa');
    renderShell();

    await waitFor(() => expect(invoke).toHaveBeenCalled());
    expect(screen.getByRole('status')).toHaveTextContent('Verificando o motor');
    expect(screen.getByRole('status')).not.toHaveTextContent('Pronto para usar');
  });
});

describe('AppShell · atividade', () => {
  const run = (runId: string, state: string) => ({
    runId,
    projectId: 'p-1',
    projectName: 'loja-api',
    kind: 'workflow',
    state,
    startedAt: '2026-11-20T10:00:00Z',
    finishedAt: null,
    live: state === 'running',
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

  const host = (summary: { running: number; awaitingApproval: number } | 'falha') => {
    (window as unknown as Record<string, unknown>).__TAURI_INTERNALS__ = {};
    type Envelope = { method?: string; params?: { states?: string[] } };
    invoke.mockImplementation(async (command: string, args?: { envelope?: Envelope }) => {
      if (command === 'engine_status') return 'ready';
      if (summary === 'falha') throw { code: 'ENGINE_STOPPED', message: 'motor parado' };
      if (args?.envelope?.method === 'timeline.read') return timelineFixture;
      const open = args?.envelope?.params?.states !== undefined;
      return {
        runs: open ? [run('r-1', 'awaiting_approval')] : [run('r-1', 'awaiting_approval'), run('r-2', 'completed')],
        nextCursor: null,
        summary: { total: 3, ...summary }
      };
    });
  };

  const toggle = () => within(screen.getByRole('banner')).getByRole('button', { name: /^Atividade/ });
  const panel = () => screen.getByRole('complementary', { name: 'Atividade' });

  it('a área Atividade avisa o que espera aprovação e o que está andando, e o nome do link diz os dois', async () => {
    host({ running: 2, awaitingApproval: 1 });
    renderShell();

    const link = await within(areas()).findByRole('link', {
      name: /Atividade.*1 aguardando aprovação, 2 em andamento/
    });
    expect(link).toHaveAttribute('href', '/execucoes');
    expect(toggle()).toHaveAccessibleName(/1 aguardando aprovação, 2 em andamento/);
  });

  it('dentro da área, o aviso acompanha o item Execuções', async () => {
    host({ running: 3, awaitingApproval: 0 });
    renderShell('/linha-do-tempo');

    expect(
      await within(areaPanel('Atividade')).findByRole('link', { name: /Execuções.*3 em andamento/ })
    ).toBeInTheDocument();
  });

  it('sem nada andando o link fica só com o nome', async () => {
    host({ running: 0, awaitingApproval: 0 });
    renderShell();

    await waitFor(() => expect(invoke).toHaveBeenCalledWith('engine_request', expect.anything()));
    expect(within(areas()).getByRole('link', { name: 'Atividade' })).toBeInTheDocument();
  });

  it('se a leitura falha o menu segue sem aviso e sem erro na tela', async () => {
    host('falha');
    renderShell();

    await waitFor(() => expect(invoke).toHaveBeenCalledWith('engine_request', expect.anything()));
    expect(within(areas()).getByRole('link', { name: 'Atividade' })).toBeInTheDocument();
    expect(screen.queryByRole('alert')).toBeNull();
  });

  it('fora do aplicativo não há motor para perguntar', () => {
    renderShell();

    expect(invoke).not.toHaveBeenCalled();
    expect(within(areas()).getByRole('link', { name: 'Atividade' })).toBeInTheDocument();
  });

  it('o painel começa fechado; o botão do topo o abre, leva o foco para ele e diz que está aberto', async () => {
    renderShell();
    expect(screen.queryByRole('complementary')).not.toBeInTheDocument();
    expect(toggle()).toHaveAttribute('aria-expanded', 'false');

    await userEvent.click(toggle());

    expect(panel()).toHaveFocus();
    expect(toggle()).toHaveAttribute('aria-expanded', 'true');
    expect(within(panel()).getByText(/Sem motor local, não há atividade nesta prévia/)).toBeInTheDocument();
    expect(within(panel()).getByRole('link', { name: 'Ver todas as execuções' })).toHaveAttribute('href', '/execucoes');
  });

  it('fechar pelo painel devolve o foco ao botão que o abre', async () => {
    renderShell();
    await userEvent.click(toggle());

    await userEvent.click(within(panel()).getByRole('button', { name: 'Fechar o painel de atividade' }));

    expect(screen.queryByRole('complementary')).not.toBeInTheDocument();
    expect(toggle()).toHaveFocus();
  });

  it('Ctrl+. abre e fecha o painel, e a escolha vale para a próxima abertura do aplicativo', async () => {
    const { unmount } = renderShell();

    await userEvent.keyboard('{Control>}.{/Control}');
    expect(panel()).toBeInTheDocument();
    unmount();

    renderShell();
    expect(panel()).toBeInTheDocument();
    // O painel que reabre sozinho não puxa o foco.
    expect(panel()).not.toHaveFocus();

    await userEvent.keyboard('{Control>}.{/Control}');
    expect(screen.queryByRole('complementary')).not.toBeInTheDocument();
  });

  it('com o motor, separa o que espera a pessoa do que já aconteceu, e cada linha abre o detalhe', async () => {
    host({ running: 0, awaitingApproval: 1 });
    renderShell();
    await userEvent.click(toggle());

    const tasks = await within(panel()).findByRole('tab', { name: 'Tarefas (1)' });
    expect(tasks).toHaveAttribute('aria-selected', 'true');
    expect(within(panel()).getByRole('button', { name: /Pedido de engenharia.*Aguardando aprovação/ })).toHaveAttribute(
      'aria-haspopup',
      'dialog'
    );

    await userEvent.click(within(panel()).getByRole('tab', { name: 'Feed' }));

    // O que está nas tarefas não se repete no feed.
    const feed = within(panel()).getByRole('list', { name: 'Execuções recentes' });
    expect(within(feed).getAllByRole('button')).toHaveLength(1);
    expect(within(feed).getByRole('button', { name: /Concluído/ })).toBeInTheDocument();
  });

  it('a divisória ajusta a largura do painel pelo teclado, dentro dos limites', async () => {
    renderShell();
    await userEvent.click(toggle());
    const divider = screen.getByRole('separator', { name: 'Largura do painel de atividade' });
    const before = Number(divider.getAttribute('aria-valuenow'));
    divider.focus();

    await userEvent.keyboard('{ArrowLeft}');
    expect(Number(divider.getAttribute('aria-valuenow'))).toBe(before + 32);

    // A janela do teste tem 1024 px: o painel não passa da metade nem fica menor que o mínimo.
    await userEvent.keyboard('{End}');
    expect(divider).toHaveAttribute('aria-valuenow', '512');
    await userEvent.keyboard('{ArrowLeft}');
    expect(divider).toHaveAttribute('aria-valuenow', '512');
    await userEvent.keyboard('{Home}{ArrowRight}');
    expect(divider).toHaveAttribute('aria-valuenow', '288');
  });

  it('a largura escolhida vale para a próxima abertura do aplicativo', async () => {
    const { unmount } = renderShell();
    await userEvent.click(toggle());
    screen.getByRole('separator', { name: 'Largura do painel de atividade' }).focus();
    await userEvent.keyboard('{ArrowLeft}{ArrowLeft}');
    unmount();

    renderShell();

    expect(screen.getByRole('separator', { name: 'Largura do painel de atividade' })).toHaveAttribute(
      'aria-valuenow',
      '400'
    );
  });

  it('uma largura guardada que não é número é ignorada', () => {
    window.localStorage.setItem('spock.activity', 'open');
    window.localStorage.setItem('spock.activityWidth', 'qualquer-coisa');
    renderShell();

    expect(screen.getByRole('separator', { name: 'Largura do painel de atividade' })).toHaveAttribute(
      'aria-valuenow',
      '336'
    );
  });

  it('a aba Projeto mostra o que aconteceu no projeto aberto, e sem projeto diz o que fazer', async () => {
    host({ running: 0, awaitingApproval: 0 });
    renderShell();
    await userEvent.click(toggle());
    await userEvent.click(await within(panel()).findByRole('tab', { name: 'Projeto' }));

    expect(within(panel()).getByText('Abra um projeto para ver o que aconteceu nele.')).toBeInTheDocument();
    // A linha do tempo só é pedida com um projeto aberto.
    expect(invoke).not.toHaveBeenCalledWith(
      'engine_request',
      expect.objectContaining({ envelope: expect.objectContaining({ method: 'timeline.read' }) })
    );

    await userEvent.click(screen.getByRole('button', { name: 'escolher' }));

    const events = await within(panel()).findByRole('list', { name: 'Acontecimentos de EmpregaNetAPI' });
    expect(within(events).getByText('Decisão: Adiar o marco em duas semanas')).toBeInTheDocument();
    expect(invoke).toHaveBeenCalledWith(
      'engine_request',
      expect.objectContaining({
        envelope: expect.objectContaining({ method: 'timeline.read', params: { projectId: 'p-1', limit: 30 } })
      })
    );
  });

  it('a aba do projeto só lê a linha do tempo quando a pessoa a abre', async () => {
    host({ running: 0, awaitingApproval: 0 });
    renderShell();
    await userEvent.click(screen.getByRole('button', { name: 'escolher' }));
    await userEvent.click(toggle());
    await within(panel()).findByRole('tab', { name: /^Tarefas/ });

    expect(invoke).not.toHaveBeenCalledWith(
      'engine_request',
      expect.objectContaining({ envelope: expect.objectContaining({ method: 'timeline.read' }) })
    );
  });

  it('não tem violações estruturais de acessibilidade com o painel aberto', async () => {
    const { container } = renderShell('/projeto');
    await userEvent.click(toggle());

    await expectNoA11yViolations(container);
  });
});

describe('AppShell · menu recolhível', () => {
  it('recolhe e expande o painel da área; a barra de áreas continua no lugar', async () => {
    renderShell('/projeto');
    const button = screen.getByRole('button', { name: 'Recolher menu' });
    expect(button).toHaveAttribute('aria-expanded', 'true');

    await userEvent.click(button);

    expect(queryAreaPanel('Engenharia')).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Expandir menu' })).toHaveAttribute('aria-expanded', 'false');
    expect(within(areas()).getByRole('link', { name: 'Engenharia' })).toHaveAttribute('href', '/projeto');

    await userEvent.click(screen.getByRole('button', { name: 'Expandir menu' }));
    expect(areaPanel('Engenharia')).toBeInTheDocument();
  });

  it('Ctrl+B faz o mesmo pelo teclado', async () => {
    renderShell('/projeto');

    await userEvent.keyboard('{Control>}b{/Control}');

    expect(queryAreaPanel('Engenharia')).not.toBeInTheDocument();
  });

  it('lembra a escolha na próxima vez que o aplicativo abre', async () => {
    const { unmount } = renderShell('/projeto');
    await userEvent.click(screen.getByRole('button', { name: 'Recolher menu' }));
    unmount();

    renderShell('/projeto');

    expect(screen.getByRole('button', { name: 'Expandir menu' })).toBeInTheDocument();
    expect(queryAreaPanel('Engenharia')).not.toBeInTheDocument();
  });

  it('na janela estreita o painel começa recolhido, abre por cima do conteúdo e fecha com Esc ou ao trocar de tela', async () => {
    narrowWindow(NARROW);
    renderShell('/projeto');
    expect(queryAreaPanel('Engenharia')).not.toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Expandir menu' }));
    const panel = areaPanel('Engenharia');
    within(panel).getByRole('link', { name: 'Configurar' }).focus();
    await userEvent.keyboard('{Escape}');
    expect(queryAreaPanel('Engenharia')).not.toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Expandir menu' }));
    await userEvent.click(within(areaPanel('Engenharia')).getByRole('link', { name: 'Configurar' }));

    expect(screen.getByTestId('where')).toHaveTextContent('/configurar');
    expect(queryAreaPanel('Engenharia')).not.toBeInTheDocument();
  });

  it('com a barra só de ícones, o nome da área aparece como dica, sem trocar o nome acessível', async () => {
    narrowWindow(NARROW, COMPACT);
    renderShell();
    const engineering = within(areas()).getByRole('link', { name: 'Engenharia' });

    await userEvent.hover(engineering);

    expect(await screen.findByRole('tooltip')).toHaveTextContent('Engenharia');
    expect(engineering).toHaveAccessibleName('Engenharia');
  });

  it('com o nome à vista, não há dica', async () => {
    renderShell();

    await userEvent.hover(within(areas()).getByRole('link', { name: 'Engenharia' }));

    expect(screen.queryByRole('tooltip')).not.toBeInTheDocument();
  });
});

describe('AppShell · painel inferior', () => {
  const panel = () => screen.getByRole('region', { name: 'Painel inferior' });
  const statusbar = () => screen.getByRole('contentinfo');

  it('Ctrl+J abre o painel; a Saída fora do aplicativo diz que não há motor', async () => {
    renderShell();
    expect(screen.queryByRole('region', { name: 'Painel inferior' })).not.toBeInTheDocument();

    await userEvent.keyboard('{Control>}j{/Control}');
    await userEvent.click(within(panel()).getByRole('tab', { name: 'Saída' }));

    expect(within(panel()).getByRole('tab', { name: 'Saída' })).toHaveAttribute('aria-selected', 'true');
    expect(within(panel()).getByText('Sem motor local, não há saída nesta prévia.')).toBeInTheDocument();
  });

  it('o atalho do terminal abre na aba Terminal, que explica onde ele roda', async () => {
    renderShell();

    await userEvent.keyboard('{Control>}`{/Control}');

    expect(within(panel()).getByRole('tab', { name: 'Terminal' })).toHaveAttribute('aria-selected', 'true');
    expect(within(panel()).getByText('O terminal roda só no aplicativo desktop.')).toBeInTheDocument();
    expect(within(statusbar()).getByRole('button', { name: 'Terminal' })).toHaveAttribute('aria-expanded', 'true');
  });

  it('o botão da barra de status alterna a aba e fecha quando ela já está à vista', async () => {
    renderShell();
    const output = within(statusbar()).getByRole('button', { name: 'Saída' });

    await userEvent.click(output);
    expect(panel()).toBeVisible();
    await userEvent.click(output);

    expect(screen.queryByRole('region', { name: 'Painel inferior' })).not.toBeInTheDocument();
    expect(output).toHaveAttribute('aria-expanded', 'false');
  });

  it('setas trocam de aba e a divisória ajusta a altura pelo teclado', async () => {
    renderShell();
    await userEvent.keyboard('{Control>}`{/Control}');
    within(panel()).getByRole('tab', { name: 'Terminal' }).focus();

    await userEvent.keyboard('{ArrowRight}');
    expect(within(panel()).getByRole('tab', { name: 'Saída' })).toHaveFocus();

    const divider = within(panel()).getByRole('separator', { name: 'Altura do painel inferior' });
    const before = Number(divider.getAttribute('aria-valuenow'));
    divider.focus();
    await userEvent.keyboard('{ArrowUp}');
    expect(Number(divider.getAttribute('aria-valuenow'))).toBeGreaterThan(before);
  });

  it('fechar com o foco dentro do painel devolve o foco ao conteúdo', async () => {
    renderShell();
    await userEvent.keyboard('{Control>}j{/Control}');
    within(panel()).getByRole('tab', { name: 'Terminal' }).focus();

    await userEvent.keyboard('{Control>}j{/Control}');

    expect(screen.getByRole('main')).toHaveFocus();
  });

  it('o atalho chega mesmo quando quem tem o foco interrompe a propagação, como o terminal faz', async () => {
    renderShell();
    await userEvent.keyboard('{Control>}j{/Control}');
    const tab = within(panel()).getByRole('tab', { name: 'Terminal' });
    tab.addEventListener('keydown', (event) => event.stopPropagation());
    tab.focus();

    await userEvent.keyboard('{Control>}j{/Control}');

    expect(screen.queryByRole('region', { name: 'Painel inferior' })).not.toBeInTheDocument();
    expect(screen.getByRole('main')).toHaveFocus();
  });

  it('lembra que estava aberto na próxima vez que o aplicativo abre', async () => {
    const { unmount } = renderShell();
    await userEvent.keyboard('{Control>}j{/Control}');
    unmount();

    renderShell();

    expect(panel()).toBeVisible();
  });

  it('Ctrl+/ mostra os atalhos de teclado', async () => {
    renderShell();

    await userEvent.keyboard('{Control>}/{/Control}');

    const sheet = await screen.findByRole('dialog', { name: 'Atalhos de teclado' });
    expect(within(sheet).getByText(/Abrir o terminal do projeto/)).toBeInTheDocument();
  });

  it('não tem violações estruturais de acessibilidade com o painel aberto', async () => {
    const { container } = renderShell();
    await userEvent.keyboard('{Control>}j{/Control}');

    await expectNoA11yViolations(container);
  });
});

describe('AppShell · item indisponível', () => {
  it('um item sem destino aparece desativado e diz o motivo', () => {
    render(
      <MemoryRouter>
        <NavEntry item={{ id: 'novo', label: 'Novidade', icon: <span />, unavailableReason: 'Em breve' }} />
      </MemoryRouter>
    );

    const item = screen.getByRole('button', { name: /Novidade/ });
    expect(item).toHaveAttribute('aria-disabled', 'true');
    expect(item).toHaveAccessibleDescription('Em breve');
  });
});
