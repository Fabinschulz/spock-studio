import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, useLocation } from 'react-router';
import { describe, expect, it, vi } from 'vitest';

import {
  ProjectSelectionProvider,
  useProjectSelectionContext,
  type ProjectPickerPort,
  type RecentProject,
  type SelectedProject
} from '@/features/project-selection';
import { AppProviders } from '@/shared/components';
import { expectNoA11yViolations } from '@/test/a11y';
import { fakeIntegrationEngine } from '@/test/fake-integration-engine';

import { PortfolioPage } from './portfolio-page';

// Os projetos do portfólio de teste: `portal` (p-000000000002) e `loja` (p-000000000001), ambos em "~".
const loja: SelectedProject = {
  projectId: 'p-000000000001',
  displayName: 'loja',
  displayPath: '~',
  recentId: 'r-loja'
};

function recent(id: string, displayName: string, overrides: Partial<RecentProject> = {}): RecentProject {
  return { id, displayName, displayPath: '~', lastOpenedMs: 1, available: true, ...overrides };
}

function Where() {
  return <p data-testid="where">{useLocation().pathname}</p>;
}

function Pick() {
  const { pick } = useProjectSelectionContext();
  return (
    <button type="button" onClick={() => void pick()}>
      escolher
    </button>
  );
}

function renderPortfolio(
  picker: ProjectPickerPort = { pick: vi.fn() },
  options: Parameters<typeof fakeIntegrationEngine>[0] = {}
) {
  const engine = fakeIntegrationEngine(options);
  const view = render(
    <AppProviders>
      <ProjectSelectionProvider picker={picker}>
        <MemoryRouter>
          <Pick />
          <PortfolioPage client={engine.client} />
          <Where />
        </MemoryRouter>
      </ProjectSelectionProvider>
    </AppProviders>
  );
  return { engine, ...view };
}

describe('Gestão · portfólio', () => {
  it('mostra cada projeto com os próprios números e quem pede atenção primeiro', async () => {
    const { engine, container } = renderPortfolio();

    const list = await screen.findByRole('list', { name: 'Projetos' });
    const [portal, loja] = within(list)
      .getAllByRole('listitem', { name: '' })
      .filter((item) => item.parentElement === list);
    expect(portal).toHaveTextContent('portal');
    expect(within(portal!).getByRole('list', { name: 'Pontos de atenção de portal' })).toHaveTextContent(
      'Nenhuma ferramenta vinculada'
    );

    expect(loja).toHaveTextContent('loja');
    expect(within(loja!).getByText('Sem pontos de atenção')).toBeInTheDocument();
    expect(loja).toHaveTextContent('Issues: 1 aberto · 1 em andamento · 6 entregues em 8 semanas');
    expect(loja).not.toHaveTextContent('prazo vencido');
    // Só leitura: a tela não pergunta nada ao modelo.
    expect(engine.calls.map((call) => call.method)).not.toContain('management.portfolioask');
    expect(engine.calls.map((call) => call.method)).toContain('management.portfolio');

    await expectNoA11yViolations(container);
  });

  it('um projeto que está entre os recentes abre pela linha e leva ao acompanhamento', async () => {
    const openRecent = vi.fn().mockResolvedValue(loja);
    renderPortfolio({
      pick: vi.fn(),
      recents: vi.fn().mockResolvedValue([recent('r-loja', 'loja')]),
      openRecent
    });

    await userEvent.click(await screen.findByRole('button', { name: 'Abrir projeto loja' }));

    expect(openRecent).toHaveBeenCalledExactlyOnceWith('r-loja');
    await waitFor(() => expect(screen.getByTestId('where')).toHaveTextContent('/gestao'));
  });

  it('só oferece abrir o que casa com um recente e explica o que fazer com o resto', async () => {
    renderPortfolio({ pick: vi.fn(), recents: vi.fn().mockResolvedValue([recent('r-loja', 'loja')]) });

    await screen.findByRole('button', { name: 'Abrir projeto loja' });
    expect(screen.queryByRole('button', { name: 'Abrir projeto portal' })).not.toBeInTheDocument();
    expect(screen.getByText(/não está entre os recentes/)).toBeInTheDocument();
  });

  it('o projeto aberto agora vem marcado e leva direto ao acompanhamento', async () => {
    renderPortfolio({ pick: vi.fn().mockResolvedValue(loja) });
    await screen.findByRole('list', { name: 'Projetos' });

    await userEvent.click(screen.getByRole('button', { name: 'escolher' }));

    const link = await screen.findByRole('link', { name: 'Ver acompanhamento de loja' });
    expect(link).toHaveAttribute('href', '/gestao');
    expect(screen.getByText('Aberto agora')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Abrir projeto loja' })).not.toBeInTheDocument();
  });

  it('a pergunta entre projetos fica no Assistente, e a tela leva até lá', async () => {
    renderPortfolio();

    await screen.findByRole('list', { name: 'Projetos' });
    expect(screen.queryByRole('form')).not.toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Assistente' })).toHaveAttribute('href', '/gestao/assistente');
  });
});
