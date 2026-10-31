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
  it('mostra o título e o texto de apoio, sem a arte do cabeçalho', () => {
    renderHome(neverCalled);

    expect(screen.getByRole('heading', { level: 1, name: 'Como podemos ajudar?' })).toBeInTheDocument();
    expect(
      screen.getByText(
        'Escolha uma atividade para começar. Vamos configurar o ambiente conforme as necessidades do seu projeto.'
      )
    ).toBeInTheDocument();
    expect(screen.queryByText('Bem-vindo(a)!')).not.toBeInTheDocument();
    expect(screen.queryByText(/Mais que ferramentas,/)).not.toBeInTheDocument();
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
    expect(screen.getByText('Analisa o repositório e configura o Harness.')).toBeInTheDocument();
    expect(
      screen.getByText('Gere especificações, backlog e roadmap a partir de documentos existentes.')
    ).toBeInTheDocument();
    expect(screen.getByText('Configure Jira, Azure DevOps, GitHub, MCPs e bancos de dados.')).toBeInTheDocument();
    expect(screen.getByText('Analise um legado e planeje sua modernização.')).toBeInTheDocument();
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
    expect(screen.getByRole('button', { name: 'Selecionar pasta' })).toHaveFocus();
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

    await user.click(screen.getByRole('button', { name: 'Selecionar pasta' }));

    expect(await screen.findByText('EmpregaNetAPI')).toBeInTheDocument();
    expect(screen.getByText('~\\…\\backend\\EmpregaNetAPI')).toBeInTheDocument();
    expect(screen.getByText('Trocar')).toBeInTheDocument();
  });

  it('mantém a tela inalterada quando o usuário cancela o diálogo', async () => {
    const picker: ProjectPickerPort = { pick: vi.fn().mockResolvedValue(null) };
    const user = userEvent.setup();
    renderHome(picker);

    await user.click(screen.getByRole('button', { name: 'Selecionar pasta' }));

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

    await user.click(screen.getByRole('button', { name: 'Selecionar pasta' }));

    expect(await screen.findByText('A pasta escolhida não pode ser usada.')).toBeInTheDocument();
  });

  it('não tenta abrir o diálogo fora do aplicativo desktop e explica o motivo', () => {
    const picker: ProjectPickerPort = { pick: vi.fn() };
    renderHome(picker, 'Disponível apenas no aplicativo desktop.');

    expect(screen.getByRole('button', { name: 'Selecionar pasta' })).toBeDisabled();
    expect(picker.pick).not.toHaveBeenCalled();
    expect(screen.getByText('Disponível apenas no aplicativo desktop.')).toBeInTheDocument();
  });

  it('a faixa de informação diz que trocar a pasta é possível e que a análise não altera arquivos', () => {
    renderHome(neverCalled);

    expect(
      screen.getByText(
        'Você pode trocar a pasta a qualquer momento. A análise inicial é somente de leitura e não altera seus arquivos.'
      )
    ).toBeInTheDocument();
  });

  it('mostra atalhos para os projetos recentes, com o último acesso, e escolher um só o seleciona', async () => {
    const user = userEvent.setup();
    const openRecent = vi.fn().mockResolvedValue(project);
    const lastOpenedMs = new Date(2026, 9, 20, 14, 32).getTime();
    renderHome({
      pick: vi.fn(),
      recents: vi
        .fn()
        .mockResolvedValue([
          { id: 'r-1', displayName: 'easymart-web', displayPath: '~/easymart-web', lastOpenedMs, available: true }
        ]),
      openRecent
    });

    const card = await screen.findByRole('button', { name: /easymart-web/ });
    expect(card).toHaveTextContent('20/10/2026 · 14:32');
    await user.click(card);

    expect(openRecent).toHaveBeenCalledWith('r-1');
    expect(screen.queryByText('JORNADA ABERTA')).not.toBeInTheDocument();
  });

  describe('seleção de projeto · estados', () => {
    it('com pasta escolhida, ela é marcada e a análise só começa pela atividade; escolher não é analisar', async () => {
      const user = userEvent.setup();
      const pick = vi.fn().mockResolvedValue(project);
      renderHome({ pick });

      expect(screen.queryByText('Pasta selecionada')).not.toBeInTheDocument();
      await user.click(screen.getByRole('button', { name: 'Selecionar pasta' }));
      expect(await screen.findByText('Pasta selecionada')).toBeInTheDocument();
      expect(screen.queryByRole('button', { name: /Analisar projeto/ })).not.toBeInTheDocument();
      expect(screen.queryByText('JORNADA ABERTA')).not.toBeInTheDocument();

      await user.click(screen.getByRole('button', { name: /Configurar projeto/ }));

      expect(await screen.findByText('JORNADA ABERTA')).toBeInTheDocument();
      expect(pick).toHaveBeenCalledTimes(1);
    });

    it('o caminho aparece inteiro no documento e a ação "Trocar" diz de qual pasta é', async () => {
      const user = userEvent.setup();
      const picker: ProjectPickerPort = { pick: vi.fn().mockResolvedValue(project) };
      renderHome(picker);

      await user.click(screen.getByRole('button', { name: 'Selecionar pasta' }));

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

      await user.click(screen.getByRole('button', { name: 'Selecionar pasta' }));

      expect(await screen.findByText(/Seleção cancelada\. Nenhuma pasta foi escolhida/)).toBeInTheDocument();
      expect(screen.queryByRole('alert')).not.toBeInTheDocument();

      await user.click(screen.getByRole('button', { name: 'Selecionar pasta' }));
      await screen.findByText('EmpregaNetAPI');
      expect(screen.queryByText(/Seleção cancelada/)).not.toBeInTheDocument();
    });

    it('pasta inválida ou inacessível vira um erro anunciado, com a causa que o host informou', async () => {
      const user = userEvent.setup();
      renderHome({
        pick: vi.fn().mockRejectedValue(new ProjectPickError('NOT_READABLE', 'Não foi possível ler a pasta.'))
      });

      await user.click(screen.getByRole('button', { name: 'Selecionar pasta' }));

      const alert = await screen.findByRole('alert');
      expect(alert).toHaveTextContent('Não foi possível usar esta pasta');
      expect(alert).toHaveTextContent('Não foi possível ler a pasta.');
    });

    it('enquanto o diálogo está aberto, a tela diz isso e não aceita um segundo clique', async () => {
      const user = userEvent.setup();
      let finish: (value: typeof project) => void = () => undefined;
      const pick = vi.fn().mockReturnValue(new Promise((resolve) => (finish = resolve)));
      renderHome({ pick });

      await user.click(screen.getByRole('button', { name: 'Selecionar pasta' }));
      expect(await screen.findByText('Abrindo o seletor de pastas…')).toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'Abrindo…' })).toBeDisabled();
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
      await user.click(screen.getByRole('button', { name: 'Selecionar pasta' }));
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
