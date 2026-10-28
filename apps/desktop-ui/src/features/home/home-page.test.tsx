import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router';
import { describe, expect, it, vi } from 'vitest';

import { ProjectPickError, ProjectSelectionProvider, type ProjectPickerPort } from '@/features/project-selection';
import { AppProviders } from '@/shared/components';
import { expectNoA11yViolations } from '@/test/a11y';

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
    expect(screen.getByText('Mariliz Studio')).toBeInTheDocument();
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

  describe('seleção de projeto · estados', () => {
    it('com pasta escolhida, a ação principal é "Analisar projeto" e leva à jornada; escolher não é analisar', async () => {
      const user = userEvent.setup();
      renderHome({ pick: vi.fn().mockResolvedValue(project) });

      expect(screen.queryByRole('button', { name: /Analisar projeto/ })).not.toBeInTheDocument();
      await user.click(screen.getByRole('button', { name: /Selecionar pasta do projeto/ }));
      await screen.findByText('Os arquivos permanecem no computador. A análise inicial é somente de leitura.');
      expect(screen.queryByText('JORNADA ABERTA')).not.toBeInTheDocument();

      await user.click(screen.getByRole('button', { name: /Analisar projeto/ }));

      expect(await screen.findByText('JORNADA ABERTA')).toBeInTheDocument();
    });

    it('o caminho aparece inteiro no documento e a ação "Trocar" diz de qual pasta é', async () => {
      const user = userEvent.setup();
      const picker: ProjectPickerPort = { pick: vi.fn().mockResolvedValue(project) };
      renderHome(picker);

      await user.click(screen.getByRole('button', { name: /Selecionar pasta do projeto/ }));

      const path = await screen.findByText(project.displayPath);
      expect(path).toHaveAttribute('title', project.displayPath);
      const change = screen.getByRole('button', { name: 'Trocar a pasta do projeto EmpregaNetAPI' });
      await user.click(change);
      expect(picker.pick).toHaveBeenCalledTimes(2);
    });

    it('seleção cancelada é dita, sem alarme, e some na próxima tentativa', async () => {
      const user = userEvent.setup();
      const pick = vi.fn().mockResolvedValueOnce(null).mockResolvedValue(project);
      renderHome({ pick });

      await user.click(screen.getByRole('button', { name: /Selecionar pasta do projeto/ }));

      expect(await screen.findByText(/Seleção cancelada\. Nenhuma pasta foi escolhida/)).toBeInTheDocument();
      expect(screen.queryByRole('alert')).not.toBeInTheDocument();

      await user.click(screen.getByRole('button', { name: /Selecionar pasta do projeto/ }));
      await screen.findByText('EmpregaNetAPI');
      expect(screen.queryByText(/Seleção cancelada/)).not.toBeInTheDocument();
    });

    it('pasta inválida ou inacessível vira um erro anunciado, com a causa que o host informou', async () => {
      const user = userEvent.setup();
      renderHome({
        pick: vi.fn().mockRejectedValue(new ProjectPickError('NOT_READABLE', 'Não foi possível ler a pasta.'))
      });

      await user.click(screen.getByRole('button', { name: /Selecionar pasta do projeto/ }));

      const alert = await screen.findByRole('alert');
      expect(alert).toHaveTextContent('Não foi possível usar esta pasta');
      expect(alert).toHaveTextContent('Não foi possível ler a pasta.');
    });

    it('enquanto o diálogo está aberto, a tela diz isso e não aceita um segundo clique', async () => {
      const user = userEvent.setup();
      let finish: (value: typeof project) => void = () => undefined;
      const pick = vi.fn().mockReturnValue(new Promise((resolve) => (finish = resolve)));
      renderHome({ pick });

      await user.click(screen.getByRole('button', { name: /Selecionar pasta do projeto/ }));
      expect(await screen.findByText('Abrindo o seletor de pastas…')).toBeInTheDocument();
      await user.click(screen.getByRole('button', { name: /Selecionar pasta do projeto/ }));
      expect(pick).toHaveBeenCalledTimes(1);

      finish(project);
      await screen.findByText('EmpregaNetAPI');
    });

    it('a tela inicial não tem violações estruturais de acessibilidade', async () => {
      const { container } = renderHome(neverCalled);

      await expectNoA11yViolations(container);
    });
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
