import { act, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { ProjectSelectionProvider, useProjectSelectionContext } from '@/features/project-selection';
import { AppProviders } from '@/shared/components';
import { expectNoA11yViolations } from '@/test/a11y';

import { AppShell } from './AppShell';

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

function renderShell(route = '/') {
  return render(
    <AppProviders>
      <ProjectSelectionProvider picker={{ pick: vi.fn().mockResolvedValue(project) }}>
        <MemoryRouter initialEntries={[route]}>
          <AppShell>
            <Pick />
            <h1>Conteúdo</h1>
          </AppShell>
        </MemoryRouter>
      </ProjectSelectionProvider>
    </AppProviders>
  );
}

beforeEach(() => {
  invoke.mockReset();
  listen.mockReset().mockResolvedValue(() => undefined);
});

afterEach(() => {
  delete (window as unknown as Record<string, unknown>).__TAURI_INTERNALS__;
});

describe('AppShell · navegação', () => {
  it('a marca leva ao início e tem nome acessível, e o conteúdo fica no <main>', () => {
    renderShell();

    expect(screen.getByRole('link', { name: 'Mariliz Studio, página inicial' })).toHaveAttribute('href', '/');
    expect(within(screen.getByRole('main')).getByRole('heading', { name: 'Conteúdo' })).toBeInTheDocument();
  });

  it('marca a página atual e deixa indisponível o que ainda não existe, com o motivo dito', () => {
    renderShell();
    const nav = screen.getByRole('navigation', { name: 'Navegação principal' });

    expect(within(nav).getByRole('link', { name: 'Início' })).toHaveAttribute('aria-current', 'page');
    for (const [name, reason] of [
      ['Catálogo', 'Em breve'],
      ['Configurações', 'Em breve'],
      ['Projetos', 'Selecione uma pasta']
    ] as const) {
      const item = within(nav).getByRole('button', { name: new RegExp(name) });
      expect(item).toHaveAttribute('aria-disabled', 'true');
      expect(item).toHaveAccessibleDescription(reason);
    }
  });

  it('"Projetos" só vira link depois de escolhida uma pasta', async () => {
    renderShell();
    const nav = screen.getByRole('navigation', { name: 'Navegação principal' });
    expect(within(nav).queryByRole('link', { name: 'Projetos' })).not.toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'escolher' }));

    expect(await within(nav).findByRole('link', { name: 'Projetos' })).toHaveAttribute('href', '/configurar');
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
    expect(screen.getByRole('link', { name: /Mariliz Studio/ })).toHaveFocus();
    await userEvent.tab();
    expect(screen.getByRole('link', { name: 'Início' })).toHaveFocus();
  });

  it('não tem violações estruturais de acessibilidade', async () => {
    const { container } = renderShell();

    await expectNoA11yViolations(container);
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

  it('um valor desconhecido do host nunca vira "pronto"', async () => {
    (window as unknown as Record<string, unknown>).__TAURI_INTERNALS__ = {};
    invoke.mockResolvedValue('qualquer-coisa');
    renderShell();

    await waitFor(() => expect(invoke).toHaveBeenCalled());
    expect(screen.getByRole('status')).toHaveTextContent('Verificando o motor');
    expect(screen.getByRole('status')).not.toHaveTextContent('Pronto para usar');
  });
});
