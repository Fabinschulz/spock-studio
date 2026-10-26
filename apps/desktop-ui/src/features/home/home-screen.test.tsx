import { Provider } from '@synthra.io/ui-kit';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import { themes } from '@/core/theme/theme';
import { ProjectPickError, type ProjectPickerPort } from '@/features/project-selection/project-picker-port';

import { HomeScreen } from './home-screen';

function renderHome(picker: ProjectPickerPort, pickerUnavailableReason?: string) {
  return render(
    <Provider theme={themes.light} cssBaseline={false}>
      <HomeScreen picker={picker} pickerUnavailableReason={pickerUnavailableReason} />
    </Provider>
  );
}

const neverCalled: ProjectPickerPort = { pick: vi.fn() };

describe('HomeScreen', () => {
  it('mostra o título, o texto de apoio e a marca', () => {
    renderHome(neverCalled);

    expect(screen.getByRole('heading', { level: 1, name: 'Como podemos ajudar?' })).toBeInTheDocument();
    expect(
      screen.getByText('Escolha uma atividade para começar. Vamos configurar o ambiente conforme as necessidades do seu projeto.')
    ).toBeInTheDocument();
    expect(screen.getByText('Mariliz AI Engineering')).toBeInTheDocument();
    expect(screen.getByText('Engenharia de software assistida por IA')).toBeInTheDocument();
  });

  it('apresenta os quatro cartões na ordem definida, com título e descrição completos', () => {
    renderHome(neverCalled);

    const cards = within(screen.getByRole('list', { name: 'Atividades' })).getAllByRole('button');
    expect(cards.map((c) => c.textContent)).toEqual([
      expect.stringContaining('Configurar projeto'),
      expect.stringContaining('Importar requisitos'),
      expect.stringContaining('Conectar ferramentas'),
      expect.stringContaining('Modernizar sistema')
    ]);
    expect(screen.getByText('Analisar o repositório e configurar o Harness.')).toBeInTheDocument();
    expect(
      screen.getByText('Gerar especificações, backlog e roadmap a partir de documentos ou ferramentas existentes.')
    ).toBeInTheDocument();
    expect(screen.getByText('Configurar Jira, Azure DevOps, GitHub, MCPs e bancos de dados.')).toBeInTheDocument();
    expect(screen.getByText('Analisar um legado e planejar sua modernização.')).toBeInTheDocument();
  });

  it('marca as jornadas ainda inexistentes como indisponíveis, sem removê-las da navegação por teclado', async () => {
    const onStart = vi.fn();
    const user = userEvent.setup();
    render(
      <Provider theme={themes.light} cssBaseline={false}>
        <HomeScreen picker={neverCalled} onStartJourney={onStart} />
      </Provider>
    );

    const first = screen.getByRole('button', { name: /Configurar projeto/ });
    expect(first).toHaveAttribute('aria-disabled', 'true');
    expect(first).not.toBeDisabled();
    expect(first).toHaveAccessibleDescription(expect.stringContaining('Em breve'));

    await user.tab();
    expect(first).toHaveFocus();
    await user.click(first);
    expect(onStart).not.toHaveBeenCalled();
  });

  it('segue a ordem de tabulação: cartões e depois o seletor de projeto', async () => {
    const user = userEvent.setup();
    renderHome(neverCalled);

    for (let i = 0; i < 4; i++) await user.tab();
    expect(screen.getByRole('button', { name: /Modernizar sistema/ })).toHaveFocus();
    await user.tab();
    expect(screen.getByRole('button', { name: /Selecionar pasta do projeto/ })).toHaveFocus();
  });

  it('exibe o texto auxiliar do seletor de projeto', () => {
    renderHome(neverCalled);

    expect(screen.getByText('Projeto')).toBeInTheDocument();
    expect(screen.getByText('Selecionar pasta do projeto')).toBeInTheDocument();
    expect(screen.getByText('Os arquivos permanecem no computador.')).toBeInTheDocument();
  });

  it('mostra o nome e o caminho abreviado depois de selecionar, com opção de trocar', async () => {
    const picker: ProjectPickerPort = {
      pick: vi.fn().mockResolvedValue({ projectId: 'p-1', displayName: 'EmpregaNetAPI', displayPath: '~\\…\\backend\\EmpregaNetAPI' })
    };
    const user = userEvent.setup();
    renderHome(picker);

    await user.click(screen.getByRole('button', { name: /Selecionar pasta do projeto/ }));

    expect(await screen.findByText('EmpregaNetAPI')).toBeInTheDocument();
    expect(screen.getByText('~\\…\\backend\\EmpregaNetAPI')).toBeInTheDocument();
    expect(screen.getByText('Trocar')).toBeInTheDocument();
  });

  it('mantém a tela inalterada quando o usuário cancela o diálogo', async () => {
    const picker: ProjectPickerPort = { pick: vi.fn().mockResolvedValue(null) };
    const user = userEvent.setup();
    renderHome(picker);

    await user.click(screen.getByRole('button', { name: /Selecionar pasta do projeto/ }));

    await waitFor(() => expect(picker.pick).toHaveBeenCalledTimes(1));
    expect(screen.getByText('Selecionar pasta do projeto')).toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('exibe a mensagem sanitizada do host quando a seleção falha', async () => {
    const picker: ProjectPickerPort = {
      pick: vi.fn().mockRejectedValue(new ProjectPickError('PATH_NOT_AUTHORIZED', 'A pasta escolhida não pode ser usada.'))
    };
    const user = userEvent.setup();
    renderHome(picker);

    await user.click(screen.getByRole('button', { name: /Selecionar pasta do projeto/ }));

    expect(await screen.findByText('A pasta escolhida não pode ser usada.')).toBeInTheDocument();
  });

  it('não tenta abrir o diálogo fora do aplicativo desktop e explica o motivo', async () => {
    const picker: ProjectPickerPort = { pick: vi.fn() };
    const user = userEvent.setup();
    renderHome(picker, 'Disponível apenas no aplicativo desktop.');

    const row = screen.getByRole('button', { name: /Selecionar pasta do projeto/ });
    expect(row).toHaveAttribute('aria-disabled', 'true');
    await user.click(row);
    expect(picker.pick).not.toHaveBeenCalled();
    expect(screen.getByText('Disponível apenas no aplicativo desktop.')).toBeInTheDocument();
  });
});
