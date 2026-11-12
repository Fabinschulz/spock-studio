import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, useLocation } from 'react-router';
import { describe, expect, it, vi } from 'vitest';

import { AppProviders } from '@/shared/components';
import { expectNoA11yViolations } from '@/test/a11y';

import { ProjectSelectionProvider } from '../context/project-selection-context';
import type { ProjectPickerPort, RecentProject, SelectedProject } from '../domain/types';
import { ProjectSwitcher } from './project-switcher';

const loja: SelectedProject = { projectId: 'p-1', displayName: 'loja', displayPath: '~\\loja', recentId: 'r-loja' };
const portal: SelectedProject = {
  projectId: 'p-2',
  displayName: 'portal',
  displayPath: '~\\portal',
  recentId: 'r-portal'
};

const recents: RecentProject[] = [
  { id: 'r-loja', displayName: 'loja', displayPath: '~\\loja', lastOpenedMs: Date.now(), available: true },
  { id: 'r-portal', displayName: 'portal', displayPath: '~\\portal', lastOpenedMs: Date.now(), available: true },
  { id: 'r-sumiu', displayName: 'antigo', displayPath: '~\\antigo', lastOpenedMs: 1, available: false }
];

function Where() {
  return <p data-testid="where">{useLocation().pathname}</p>;
}

function renderSwitcher(overrides: Partial<ProjectPickerPort> = {}, unavailableReason?: string) {
  const picker: ProjectPickerPort = {
    pick: vi.fn().mockResolvedValue(portal),
    recents: vi.fn().mockResolvedValue(recents),
    openRecent: vi.fn().mockImplementation(async (id: string) => (id === 'r-loja' ? loja : portal)),
    ...overrides
  };
  const view = render(
    <AppProviders>
      <ProjectSelectionProvider picker={picker} unavailableReason={unavailableReason}>
        <MemoryRouter>
          <ProjectSwitcher />
          <Where />
        </MemoryRouter>
      </ProjectSelectionProvider>
    </AppProviders>
  );
  return { picker, ...view };
}

const trigger = () => screen.getByRole('button', { name: /^Projeto: / });

describe('Seletor de projeto', () => {
  it('sem projeto aberto, diz isso em vez de mostrar um nome', () => {
    renderSwitcher();

    expect(trigger()).toHaveTextContent('Nenhum projeto aberto');
    expect(trigger()).toHaveAttribute('aria-expanded', 'false');
  });

  it('abre a lista de recentes e troca de projeto sem sair da tela', async () => {
    const { picker } = renderSwitcher();
    await waitFor(() => expect(picker.recents).toHaveBeenCalled());

    await userEvent.click(trigger());
    expect(trigger()).toHaveAttribute('aria-expanded', 'true');
    const panel = screen.getByRole('group', { name: 'Trocar de projeto' });
    expect(trigger()).toHaveAttribute('aria-controls', panel.id);

    await userEvent.click(await within(panel).findByRole('button', { name: /loja/ }));

    expect(picker.openRecent).toHaveBeenCalledExactlyOnceWith('r-loja');
    await waitFor(() => expect(trigger()).toHaveTextContent('loja'));
    expect(screen.queryByRole('group', { name: 'Trocar de projeto' })).not.toBeInTheDocument();
    expect(screen.getByTestId('where')).toHaveTextContent('/');
  });

  it('marca o projeto aberto no painel e deixa indisponível o que a pasta já não tem', async () => {
    renderSwitcher();
    await userEvent.click(trigger());
    const panel = screen.getByRole('group', { name: 'Trocar de projeto' });
    await userEvent.click(await within(panel).findByRole('button', { name: /loja/ }));
    await userEvent.click(trigger());

    const reopened = screen.getByRole('group', { name: 'Trocar de projeto' });
    expect(within(reopened).getByRole('button', { name: /loja/ })).toHaveAttribute('aria-current', 'true');
    const missing = within(reopened).getByRole('button', { name: /antigo/ });
    expect(missing).toHaveAttribute('aria-disabled', 'true');
    expect(missing).toHaveAccessibleDescription(/não existe mais/);
  });

  it('Esc fecha o painel e devolve o foco ao botão', async () => {
    renderSwitcher();
    await userEvent.click(trigger());
    expect(screen.getByRole('group', { name: 'Trocar de projeto' })).toBeInTheDocument();

    await userEvent.keyboard('{Escape}');

    expect(screen.queryByRole('group', { name: 'Trocar de projeto' })).not.toBeInTheDocument();
    expect(trigger()).toHaveFocus();
  });

  it('clicar fora fecha o painel', async () => {
    renderSwitcher();
    await userEvent.click(trigger());

    await userEvent.click(screen.getByTestId('where'));

    expect(screen.queryByRole('group', { name: 'Trocar de projeto' })).not.toBeInTheDocument();
  });

  it('"Abrir outra pasta" usa o diálogo nativo e atualiza o projeto aberto', async () => {
    const { picker } = renderSwitcher();
    await userEvent.click(trigger());

    await userEvent.click(screen.getByRole('button', { name: 'Abrir outra pasta' }));

    expect(picker.pick).toHaveBeenCalledTimes(1);
    await waitFor(() => expect(trigger()).toHaveTextContent('portal'));
  });

  it('"Todos os projetos" leva ao portfólio e fecha o painel', async () => {
    renderSwitcher();
    await userEvent.click(trigger());

    await userEvent.click(screen.getByRole('button', { name: 'Todos os projetos' }));

    expect(screen.getByTestId('where')).toHaveTextContent('/portfolio');
    expect(screen.queryByRole('group', { name: 'Trocar de projeto' })).not.toBeInTheDocument();
  });

  it('fora do aplicativo desktop não finge que dá para abrir uma pasta', async () => {
    const { picker } = renderSwitcher({ recents: undefined }, 'Disponível apenas no aplicativo desktop.');
    await userEvent.click(trigger());

    const open = screen.getByRole('button', { name: 'Abrir outra pasta' });
    expect(open).toHaveAttribute('aria-disabled', 'true');
    expect(open).toHaveAccessibleDescription('Disponível apenas no aplicativo desktop.');
    await userEvent.click(open);
    expect(picker.pick).not.toHaveBeenCalled();
  });

  it('mostra o erro de abrir um projeto dentro do painel, perto da ação', async () => {
    renderSwitcher({ openRecent: vi.fn().mockRejectedValue(new Error('falhou')) });
    await userEvent.click(trigger());
    const panel = screen.getByRole('group', { name: 'Trocar de projeto' });

    await userEvent.click(await within(panel).findByRole('button', { name: /loja/ }));

    expect(await within(panel).findByRole('alert')).toHaveTextContent('Não foi possível abrir o projeto.');
    expect(screen.getByRole('group', { name: 'Trocar de projeto' })).toBeInTheDocument();
  });

  it('não tem violações estruturais de acessibilidade, fechado e aberto', async () => {
    const { container } = renderSwitcher();
    await expectNoA11yViolations(container);

    await userEvent.click(trigger());
    await screen.findByRole('button', { name: /loja/ });
    await expectNoA11yViolations(container);
  });
});
