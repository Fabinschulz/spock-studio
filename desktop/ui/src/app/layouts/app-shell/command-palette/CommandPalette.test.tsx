import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, useLocation } from 'react-router';
import { describe, expect, it, vi } from 'vitest';

import { ProjectSelectionProvider, type ProjectPickerPort } from '@/features/project-selection';
import { AppProviders } from '@/shared/components';
import { expectNoA11yViolations } from '@/test/a11y';

import { CommandPalette } from './CommandPalette';

const recentProject = {
  id: 'r-1',
  displayName: 'loja-api',
  displayPath: '~\\dev\\loja-api',
  lastOpenedMs: 1,
  available: true
};

function Where() {
  return <p data-testid="where">{useLocation().pathname}</p>;
}

function renderPalette(picker: Partial<ProjectPickerPort> = {}) {
  const onClose = vi.fn();
  const port: ProjectPickerPort = { pick: vi.fn(), ...picker };
  render(
    <AppProviders>
      <ProjectSelectionProvider picker={port}>
        <MemoryRouter>
          <Where />
          <CommandPalette open onClose={onClose} />
        </MemoryRouter>
      </ProjectSelectionProvider>
    </AppProviders>
  );
  return { onClose };
}

describe('CommandPalette', () => {
  it('abre com o campo focado e todas as telas à escolha', () => {
    renderPalette();

    const field = screen.getByRole('combobox', { name: 'Tela ou projeto' });
    expect(field).toHaveFocus();
    expect(screen.getByRole('option', { name: /Harness/ })).toBeInTheDocument();
    expect(screen.getByRole('option', { name: /Portfólio/ })).toBeInTheDocument();
  });

  it('filtra ao digitar, sem exigir acento', async () => {
    renderPalette();

    await userEvent.type(screen.getByRole('combobox'), 'catalogo');

    expect(screen.getAllByRole('option')).toHaveLength(1);
    expect(screen.getByRole('option', { name: /Catálogo/ })).toBeInTheDocument();
  });

  it('diz quando nada combina, em vez de mostrar uma lista vazia', async () => {
    renderPalette();

    await userEvent.type(screen.getByRole('combobox'), 'zzz');

    expect(screen.queryByRole('option')).not.toBeInTheDocument();
    expect(screen.getByRole('status')).toHaveTextContent('Nada encontrado para "zzz".');
  });

  it('Enter abre a opção ativa e fecha a paleta', async () => {
    const { onClose } = renderPalette();

    await userEvent.type(screen.getByRole('combobox'), 'harness{Enter}');

    expect(screen.getByTestId('where')).toHaveTextContent('/projeto/harness');
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('as setas mudam a opção ativa, que o campo aponta por aria-activedescendant', async () => {
    renderPalette();
    const field = screen.getByRole('combobox');
    const [first, second] = screen.getAllByRole('option');
    expect(first).toHaveAttribute('aria-selected', 'true');

    await userEvent.keyboard('{ArrowDown}');

    expect(second).toHaveAttribute('aria-selected', 'true');
    expect(field).toHaveAttribute('aria-activedescendant', second!.id);
    await userEvent.keyboard('{ArrowUp}{ArrowUp}');
    expect(screen.getAllByRole('option').at(-1)).toHaveAttribute('aria-selected', 'true');
  });

  it('o clique também abre a opção', async () => {
    renderPalette();

    await userEvent.click(screen.getByRole('option', { name: /Auditoria/ }));

    expect(screen.getByTestId('where')).toHaveTextContent('/auditoria');
  });

  it('oferece os projetos recentes e abre o escolhido, sem mandar caminho', async () => {
    const openRecent = vi.fn().mockResolvedValue({ projectId: 'p', displayName: 'loja-api', displayPath: '~' });
    renderPalette({ recents: vi.fn().mockResolvedValue([recentProject]), openRecent });

    await userEvent.type(screen.getByRole('combobox'), 'dev loja');
    await userEvent.click(await screen.findByRole('option', { name: /loja-api/ }));

    expect(openRecent).toHaveBeenCalledWith('r-1');
  });

  it('Esc fecha', async () => {
    const { onClose } = renderPalette();

    await userEvent.keyboard('{Escape}');

    expect(onClose).toHaveBeenCalled();
  });

  it('não tem violações estruturais de acessibilidade', async () => {
    renderPalette();

    await expectNoA11yViolations(document.body);
  });
});
