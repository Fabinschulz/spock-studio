import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router';
import { describe, expect, it, vi } from 'vitest';

import { ProjectPickError, ProjectSelectionProvider, type ProjectPickerPort } from '@/features/project-selection';
import { AppProviders } from '@/shared/components';

import { HomePage } from './home-page';

const project = { projectId: 'p-1', displayName: 'EmpregaNetAPI', displayPath: '~\\…\\backend\\EmpregaNetAPI' };

function renderHome(picker: ProjectPickerPort, unavailableReason?: string) {
  return render(
    <AppProviders>
      <ProjectSelectionProvider picker={picker} unavailableReason={unavailableReason}>
        <MemoryRouter initialEntries={['/']}>
          <Routes>
            <Route path="/" element={<HomePage />} />
            <Route path="/configurar" element={<p>JORNADA ABERTA</p>} />
          </Routes>
        </MemoryRouter>
      </ProjectSelectionProvider>
    </AppProviders>
  );
}

const neverCalled: ProjectPickerPort = { pick: vi.fn() };

describe('HomePage', () => {
  it('mostra o título, o texto de apoio e a marca', () => {
    renderHome(neverCalled);

    expect(screen.getByRole('heading', { level: 1, name: 'Como podemos ajudar?' })).toBeInTheDocument();
    expect(
      screen.getByText(
        'Escolha uma atividade para começar. Vamos configurar o ambiente conforme as necessidades do seu projeto.'
      )
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

  it('só "Configurar projeto" está disponível; as demais jornadas dizem "Em breve" e continuam focáveis', async () => {
    const user = userEvent.setup();
    renderHome(neverCalled);

    expect(screen.getByRole('button', { name: /Configurar projeto/ })).not.toHaveAttribute('aria-disabled');
    for (const name of [/Importar requisitos/, /Conectar ferramentas/, /Modernizar sistema/]) {
      const card = screen.getByRole('button', { name });
      expect(card).toHaveAttribute('aria-disabled', 'true');
      expect(card).not.toBeDisabled();
      expect(card).toHaveAccessibleDescription(expect.stringContaining('Em breve'));
    }

    await user.click(screen.getByRole('button', { name: /Importar requisitos/ }));
    expect(screen.queryByText('JORNADA ABERTA')).not.toBeInTheDocument();
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
    const user = userEvent.setup();
    renderHome({ pick: vi.fn().mockResolvedValue(project) });

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
      pick: vi.fn().mockRejectedValue(new ProjectPickError('PATH_NOT_ALLOWED', 'A pasta escolhida não pode ser usada.'))
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

  describe('jornada Configurar projeto', () => {
    it('sem pasta escolhida, abre o diálogo e começa a jornada ao escolher', async () => {
      const picker: ProjectPickerPort = { pick: vi.fn().mockResolvedValue(project) };
      const user = userEvent.setup();
      renderHome(picker);

      await user.click(screen.getByRole('button', { name: /Configurar projeto/ }));

      expect(picker.pick).toHaveBeenCalledTimes(1);
      expect(await screen.findByText('JORNADA ABERTA')).toBeInTheDocument();
    });

    it('com pasta já escolhida, não pergunta de novo', async () => {
      const picker: ProjectPickerPort = { pick: vi.fn().mockResolvedValue(project) };
      const user = userEvent.setup();
      renderHome(picker);
      await user.click(screen.getByRole('button', { name: /Selecionar pasta do projeto/ }));
      await screen.findByText('Trocar');

      await user.click(screen.getByRole('button', { name: /Configurar projeto/ }));

      expect(await screen.findByText('JORNADA ABERTA')).toBeInTheDocument();
      expect(picker.pick).toHaveBeenCalledTimes(1);
    });

    it('se a pessoa cancela o diálogo, fica na tela inicial', async () => {
      const picker: ProjectPickerPort = { pick: vi.fn().mockResolvedValue(null) };
      const user = userEvent.setup();
      renderHome(picker);

      await user.click(screen.getByRole('button', { name: /Configurar projeto/ }));

      await waitFor(() => expect(picker.pick).toHaveBeenCalled());
      expect(screen.queryByText('JORNADA ABERTA')).not.toBeInTheDocument();
      expect(screen.getByRole('heading', { level: 1, name: 'Como podemos ajudar?' })).toBeInTheDocument();
    });
  });
});
