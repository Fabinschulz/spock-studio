import { act, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, useLocation } from 'react-router';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { ProjectSelectionProvider, useProjectSelectionContext } from '@/features/project-selection';
import { AppProviders } from '@/shared/components';
import { expectNoA11yViolations } from '@/test/a11y';

import { AppShell } from './AppShell';
import { describeRunActivity } from './SideNav';

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

function renderShell(route = '/') {
  return render(
    <AppProviders>
      <ProjectSelectionProvider picker={{ pick: vi.fn().mockResolvedValue(project) }}>
        <MemoryRouter initialEntries={[route]}>
          <AppShell>
            <Pick />
            <Where />
            <h1>Conteúdo</h1>
          </AppShell>
        </MemoryRouter>
      </ProjectSelectionProvider>
    </AppProviders>
  );
}

const navigation = () => screen.getByRole('navigation', { name: 'Navegação principal' });

beforeEach(() => {
  window.localStorage.clear();
  invoke.mockReset();
  listen.mockReset().mockResolvedValue(() => undefined);
});

afterEach(() => {
  delete (window as unknown as Record<string, unknown>).__TAURI_INTERNALS__;
});

describe('AppShell · navegação', () => {
  it('a marca leva ao início e tem nome acessível, e o conteúdo fica no <main>', () => {
    renderShell();

    expect(screen.getByRole('link', { name: 'Spock Studio, página inicial' })).toHaveAttribute('href', '/');
    expect(within(screen.getByRole('main')).getByRole('heading', { name: 'Conteúdo' })).toBeInTheDocument();
  });

  it('marca a página atual e deixa indisponível o que ainda não existe, com o motivo dito', () => {
    renderShell();
    const nav = navigation();

    expect(within(nav).getByRole('link', { name: 'Início' })).toHaveAttribute('aria-current', 'page');
    expect(within(nav).getByRole('link', { name: 'Visão geral' })).toHaveAttribute('href', '/projeto');
    // "Configurar" abre a primeira etapa da jornada, onde se escolhe a pasta: sempre existe.
    expect(within(nav).getByRole('link', { name: 'Configurar' })).toHaveAttribute('href', '/configurar');
    expect(within(nav).getByRole('link', { name: 'Configurações' })).toHaveAttribute('href', '/configuracoes');
    const catalog = within(nav).getByRole('button', { name: /Catálogo/ });
    expect(catalog).toHaveAttribute('aria-disabled', 'true');
    expect(catalog).toHaveAccessibleDescription('Em breve');
  });

  it('mostra só o ambiente atual no menu e deixa as integrações fora dos dois', () => {
    renderShell();
    const nav = navigation();

    const engineering = within(nav).getByRole('list', { name: 'Engenharia' });
    expect(within(engineering).getByRole('link', { name: 'Visão geral' })).toHaveAttribute('href', '/projeto');
    expect(within(engineering).getByRole('link', { name: 'Harness' })).toHaveAttribute('href', '/projeto/harness');
    expect(within(engineering).getByRole('link', { name: 'Conhecimento' })).toHaveAttribute(
      'href',
      '/projeto/conhecimento'
    );
    expect(within(engineering).getByRole('link', { name: 'Configurar' })).toHaveAttribute('href', '/configurar');
    expect(within(engineering).getByRole('link', { name: 'Pedido de engenharia' })).toHaveAttribute('href', '/pedido');
    expect(within(nav).queryByRole('list', { name: 'Gestão' })).not.toBeInTheDocument();

    const platform = within(nav).getByRole('list', { name: 'Plataforma' });
    expect(within(platform).getByRole('link', { name: 'Integrações' })).toHaveAttribute('href', '/integracoes');
  });

  it('em Gestão, o menu traz as telas de Gestão e some com as de Engenharia', () => {
    renderShell('/gestao');
    const nav = navigation();

    const management = within(nav).getByRole('list', { name: 'Gestão' });
    expect(within(management).getByRole('link', { name: 'Acompanhamento' })).toHaveAttribute('href', '/gestao');
    expect(within(management).getByRole('link', { name: 'Portfólio' })).toHaveAttribute('href', '/portfolio');
    expect(within(management).getByRole('link', { name: 'Assistente' })).toHaveAttribute('href', '/gestao/assistente');
    expect(within(nav).queryByRole('list', { name: 'Engenharia' })).not.toBeInTheDocument();
    expect(within(nav).queryByRole('link', { name: 'Configurar' })).not.toBeInTheDocument();
    expect(within(nav).getByRole('list', { name: 'Plataforma' })).toBeInTheDocument();
  });

  it('nenhum item do menu repete o ícone de outro', () => {
    renderShell();
    const nav = navigation();

    const icons = within(nav)
      .getAllByRole('link')
      .map((link) => link.querySelector('svg')?.innerHTML);
    expect(icons.every(Boolean)).toBe(true);
    expect(new Set(icons).size).toBe(icons.length);
  });

  it('a Visão geral só fica marcada em /projeto, não nas telas que ficam abaixo dele', () => {
    renderShell('/projeto/harness');
    const nav = navigation();

    expect(within(nav).getByRole('link', { name: 'Harness' })).toHaveAttribute('aria-current', 'page');
    expect(within(nav).getByRole('link', { name: 'Visão geral' })).not.toHaveAttribute('aria-current');
    expect(within(nav).getByRole('list', { name: 'Engenharia' })).toBeInTheDocument();
  });

  it('trocar de tela volta o conteúdo ao topo e leva o foco para ele; ao abrir, não mexe no foco', async () => {
    renderShell();
    const main = screen.getByRole('main');
    main.scrollTo = vi.fn();
    expect(main).not.toHaveFocus();

    await userEvent.click(within(navigation()).getByRole('link', { name: 'Harness' }));

    expect(screen.getByTestId('where')).toHaveTextContent('/projeto/harness');
    expect(main.scrollTo).toHaveBeenCalledWith({ top: 0 });
    expect(main).toHaveFocus();
  });

  it('o atalho "Ir para o conteúdo" move o foco para a área principal, sem mudar a rota', async () => {
    renderShell();

    await userEvent.click(screen.getByRole('button', { name: 'Ir para o conteúdo' }));

    expect(screen.getByRole('main')).toHaveFocus();
  });

  it('a navegação por teclado segue a ordem do documento: atalho, marca, menu', async () => {
    renderShell();

    await userEvent.tab();
    expect(screen.getByRole('button', { name: 'Ir para o conteúdo' })).toHaveFocus();
    await userEvent.tab();
    expect(screen.getByRole('link', { name: /Spock Studio/ })).toHaveFocus();
    await userEvent.tab();
    expect(screen.getByRole('link', { name: 'Início' })).toHaveFocus();
  });

  it('não tem violações estruturais de acessibilidade', async () => {
    const { container } = renderShell();

    await expectNoA11yViolations(container);
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

  it('marca o ambiente atual por estado, não só por cor', () => {
    renderShell();
    const environments = within(screen.getByRole('banner')).getByRole('navigation', { name: 'Ambiente' });

    expect(within(environments).getByRole('link', { name: 'Engenharia' })).toHaveAttribute('aria-current', 'true');
    expect(within(environments).getByRole('link', { name: 'Gestão' })).not.toHaveAttribute('aria-current');
    expect(within(environments).getByRole('link', { name: 'Gestão' })).toHaveAttribute('href', '/gestao');
  });

  it('trocar de ambiente leva à tela dele, troca o menu e não troca o projeto', async () => {
    renderShell();
    await userEvent.click(screen.getByRole('button', { name: 'escolher' }));
    const header = screen.getByRole('banner');
    await within(header).findByRole('button', { name: 'Projeto: EmpregaNetAPI' });

    await userEvent.click(within(header).getByRole('link', { name: 'Gestão' }));

    expect(screen.getByTestId('where')).toHaveTextContent('/gestao');
    expect(within(header).getByRole('link', { name: 'Gestão' })).toHaveAttribute('aria-current', 'true');
    expect(within(navigation()).getByRole('list', { name: 'Gestão' })).toBeInTheDocument();
    expect(within(header).getByRole('button', { name: 'Projeto: EmpregaNetAPI' })).toBeInTheDocument();
  });

  it('numa tela comum, como Integrações, mantém o ambiente em que a pessoa estava', async () => {
    renderShell('/gestao');
    const nav = navigation();

    await userEvent.click(within(nav).getByRole('link', { name: 'Integrações' }));

    expect(screen.getByTestId('where')).toHaveTextContent('/integracoes');
    expect(within(screen.getByRole('banner')).getByRole('link', { name: 'Gestão' })).toHaveAttribute(
      'aria-current',
      'true'
    );
    expect(within(nav).getByRole('list', { name: 'Gestão' })).toBeInTheDocument();
  });

  it('lembra o último ambiente na próxima vez que o aplicativo abre', () => {
    window.localStorage.setItem('spock.environment', 'management');
    renderShell();

    expect(within(screen.getByRole('banner')).getByRole('link', { name: 'Gestão' })).toHaveAttribute(
      'aria-current',
      'true'
    );
  });

  it('grava o ambiente ao entrar numa tela dele', async () => {
    renderShell('/gestao');

    await waitFor(() => expect(window.localStorage.getItem('spock.environment')).toBe('management'));
  });

  it('um valor guardado que não é um ambiente é ignorado', () => {
    window.localStorage.setItem('spock.environment', 'qualquer-coisa');
    renderShell();

    expect(within(screen.getByRole('banner')).getByRole('link', { name: 'Engenharia' })).toHaveAttribute(
      'aria-current',
      'true'
    );
  });

  it('o estado do motor aparece uma vez só, no rodapé do menu, junto da versão do pacote', () => {
    renderShell();

    const rail = screen.getByRole('complementary');
    expect(within(rail).getByRole('status')).toHaveTextContent('Prévia no navegador');
    expect(within(rail).getByText(/^v\d+\.\d+\.\d+/)).toBeInTheDocument();
    const header = screen.getByRole('banner');
    expect(within(header).queryByText('Online')).not.toBeInTheDocument();
    expect(within(header).queryByText(/^v\d+\.\d+\.\d+/)).not.toBeInTheDocument();
    expect(within(header).queryByText('Engenharia de software assistida por IA')).not.toBeInTheDocument();
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

describe('AppShell · execuções no menu', () => {
  const host = (summary: { running: number; awaitingApproval: number } | 'falha') => {
    (window as unknown as Record<string, unknown>).__TAURI_INTERNALS__ = {};
    invoke.mockImplementation(async (command: string) => {
      if (command === 'engine_status') return 'ready';
      if (summary === 'falha') throw { code: 'ENGINE_STOPPED', message: 'motor parado' };
      return { runs: [], nextCursor: null, summary: { total: 3, ...summary } };
    });
  };

  it('avisa o que espera aprovação e o que está andando, e o nome do link diz os dois', async () => {
    host({ running: 2, awaitingApproval: 1 });
    renderShell();

    const link = await within(navigation()).findByRole('link', {
      name: /Execuções.*1 aguardando aprovação, 2 em andamento/
    });
    expect(link).toHaveAttribute('href', '/execucoes');
  });

  it('sem aprovação pendente mostra só o que anda', async () => {
    host({ running: 3, awaitingApproval: 0 });
    renderShell();

    expect(await within(navigation()).findByRole('link', { name: /Execuções.*3 em andamento/ })).toBeInTheDocument();
  });

  it('sem nada andando o link fica só com o nome', async () => {
    host({ running: 0, awaitingApproval: 0 });
    renderShell();

    await waitFor(() => expect(invoke).toHaveBeenCalledWith('engine_request', expect.anything()));
    expect(within(navigation()).getByRole('link', { name: 'Execuções' })).toBeInTheDocument();
  });

  it('se a leitura falha o menu segue sem aviso e sem erro na tela', async () => {
    host('falha');
    renderShell();

    await waitFor(() => expect(invoke).toHaveBeenCalledWith('engine_request', expect.anything()));
    expect(within(navigation()).getByRole('link', { name: 'Execuções' })).toBeInTheDocument();
    expect(screen.queryByRole('alert')).toBeNull();
  });

  it('fora do aplicativo não há motor para perguntar', () => {
    renderShell();

    expect(invoke).not.toHaveBeenCalled();
    expect(within(navigation()).getByRole('link', { name: 'Execuções' })).toBeInTheDocument();
  });

  it('a descrição do aviso prefere a aprovação pendente e some quando não há nada', () => {
    expect(describeRunActivity(null)).toBeUndefined();
    expect(describeRunActivity({ running: 0, awaitingApproval: 0 })).toBeUndefined();
    expect(describeRunActivity({ running: 2, awaitingApproval: 1 })).toEqual({
      text: '1',
      label: '1 aguardando aprovação, 2 em andamento',
      tone: 'warning'
    });
    expect(describeRunActivity({ running: 2, awaitingApproval: 0 })).toEqual({
      text: '2',
      label: '2 em andamento',
      tone: 'info'
    });
  });
});
