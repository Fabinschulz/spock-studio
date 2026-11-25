import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, useLocation } from 'react-router';
import { describe, expect, it, vi } from 'vitest';

import { ProjectSelectionProvider, type ProjectPickerPort } from '@/features/project-selection';
import { AppProviders } from '@/shared/components';
import { readHandoff } from '@/shared/routing';
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
  const { pathname, state } = useLocation();
  return (
    <>
      <p data-testid="where">{pathname}</p>
      <p data-testid="handoff">{readHandoff(state) ?? ''}</p>
    </>
  );
}

function renderPalette(picker: Partial<ProjectPickerPort> = {}) {
  const onClose = vi.fn();
  const onShellAction = vi.fn();
  const port: ProjectPickerPort = { pick: vi.fn().mockResolvedValue(null), ...picker };
  render(
    <AppProviders>
      <ProjectSelectionProvider picker={port}>
        <MemoryRouter>
          <Where />
          <CommandPalette open onClose={onClose} onShellAction={onShellAction} />
        </MemoryRouter>
      </ProjectSelectionProvider>
    </AppProviders>
  );
  return { onClose, onShellAction, port };
}

describe('CommandPalette', () => {
  it('abre com o campo focado e todas as telas à escolha', () => {
    renderPalette();

    const field = screen.getByRole('combobox', { name: 'Tela, projeto, comando ou pedido' });
    expect(field).toHaveFocus();
    expect(screen.getByRole('option', { name: /^Padrões do projeto/ })).toBeInTheDocument();
    expect(screen.getByRole('option', { name: /^Portfólio/ })).toBeInTheDocument();
    // A aba vira opção com o nome dela, então o nome que a tela tinha no menu continua achável.
    expect(screen.getByRole('option', { name: /^Auditoria/ })).toBeInTheDocument();
    expect(screen.getByRole('option', { name: /^Da ideia ao projeto/ })).toBeInTheDocument();
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

  it('agrupa o que oferece: ações do aplicativo, telas e projetos', async () => {
    renderPalette({ recents: vi.fn().mockResolvedValue([recentProject]) });

    const actions = screen.getByRole('group', { name: 'Ações' });
    expect(within(actions).getByRole('option', { name: /Abrir pasta de projeto/ })).toBeInTheDocument();
    expect(
      within(screen.getByRole('group', { name: 'Telas' })).getByRole('option', { name: /^Portfólio/ })
    ).toBeInTheDocument();
    expect(
      within(await screen.findByRole('group', { name: 'Projetos' })).getByRole('option', { name: /loja-api/ })
    ).toBeInTheDocument();
  });

  it('acha a tela pelo nome técnico e diz para que ela serve', async () => {
    renderPalette();

    await userEvent.type(screen.getByRole('combobox'), 'harness');

    const option = screen.getByRole('option', { name: /^Padrões do projeto/ });
    expect(option).toHaveTextContent('Regras, verificações e assistentes de IA aplicados ao projeto.');
  });

  it('uma ação do aplicativo é entregue ao shell', async () => {
    const { onShellAction, onClose } = renderPalette();

    await userEvent.type(screen.getByRole('combobox'), 'terminal{Enter}');

    expect(onShellAction).toHaveBeenCalledWith('terminal');
    expect(onClose).toHaveBeenCalled();
  });

  it('abrir pasta de projeto chama o seletor de pasta', async () => {
    const { port } = renderPalette();

    await userEvent.type(screen.getByRole('combobox'), 'abrir pasta{Enter}');

    expect(port.pick).toHaveBeenCalled();
  });

  it('uma frase vira pedido: oferece os caminhos, o mais provável primeiro, e as telas relacionadas', async () => {
    renderPalette();

    await userEvent.type(screen.getByRole('combobox'), 'mostre os riscos dos meus projetos');

    const ask = screen.getByRole('group', { name: 'Pedir ao Spock Studio' });
    // A frase é um pedido: o caminho mais provável já vem marcado, e Enter o segue.
    const first = screen.getAllByRole('option')[0]!;
    expect(first).toHaveAttribute('aria-selected', 'true');
    expect(first).toHaveTextContent('Pergunta sobre o projeto');
    expect(
      within(ask)
        .getAllByRole('option')
        .map((option) => option.textContent)
    ).toEqual([
      expect.stringContaining('Pergunta sobre o projeto'),
      expect.stringContaining('Pedido de engenharia'),
      expect.stringContaining('Nova ideia')
    ]);
    expect(
      within(screen.getByRole('group', { name: 'Telas' })).getByRole('option', { name: /^Portfólio/ })
    ).toBeInTheDocument();
  });

  it('uma frase que é o nome de um comando continua sendo esse comando', async () => {
    const { port } = renderPalette();

    await userEvent.type(screen.getByRole('combobox'), 'abrir pasta de projeto{Enter}');

    expect(port.pick).toHaveBeenCalled();
    expect(screen.getByTestId('where')).toHaveTextContent(/^\/$/);
  });

  it('escolher um caminho leva o texto à tela que o trata, depois de garantir um projeto', async () => {
    const project = { projectId: 'p', displayName: 'loja-api', displayPath: '~' };
    const { port } = renderPalette({ pick: vi.fn().mockResolvedValue(project) });

    await userEvent.type(screen.getByRole('combobox'), 'trocar o contrato de autenticação');
    await userEvent.click(screen.getByRole('option', { name: /^Pedido de engenharia/ }));

    expect(port.pick).toHaveBeenCalled();
    await waitFor(() => expect(screen.getByTestId('where')).toHaveTextContent('/pedido'));
    expect(screen.getByTestId('handoff')).toHaveTextContent('trocar o contrato de autenticação');
  });

  it('sem projeto e sem escolher uma pasta, o pedido não segue', async () => {
    renderPalette();

    await userEvent.type(screen.getByRole('combobox'), 'trocar o contrato de autenticação');
    await userEvent.click(screen.getByRole('option', { name: /^Pedido de engenharia/ }));

    expect(screen.getByTestId('where')).toHaveTextContent(/^\/$/);
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
