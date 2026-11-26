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
import portfolioFixture from '@contracts/fixtures/management/portfolio.json';
import { expectNoA11yViolations } from '@/test/a11y';
import { fakeIntegrationEngine } from '@/test/fake-integration-engine';

import { PortfolioPage } from './portfolio-page';

// Os projetos do portfólio de teste: `loja` (p-000000000001, com ferramenta) e `portal` (p-000000000002, sem), ambos em "~".
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

function Probe() {
  const { pick, switcherOpen } = useProjectSelectionContext();
  return (
    <>
      <button type="button" onClick={() => void pick()}>
        escolher
      </button>
      <p data-testid="switcher">{String(switcherOpen)}</p>
    </>
  );
}

function renderPortfolio(
  picker: ProjectPickerPort = { pick: vi.fn() },
  options: Parameters<typeof fakeIntegrationEngine>[0] = {},
  portfolio: unknown = portfolioFixture
) {
  const engine = fakeIntegrationEngine(options);
  const original = engine.client.request.bind(engine.client);
  // A chamada passa pelo motor de teste, que a registra; só a resposta é trocada.
  engine.client.request = (async (method: string, ...rest: unknown[]) => {
    const reply = await (original as (...args: unknown[]) => Promise<unknown>)(method, ...rest);
    return method === 'management.portfolio' ? portfolio : reply;
  }) as typeof engine.client.request;
  const view = render(
    <AppProviders>
      <ProjectSelectionProvider picker={picker}>
        <MemoryRouter>
          <Probe />
          <PortfolioPage client={engine.client} />
          <Where />
        </MemoryRouter>
      </ProjectSelectionProvider>
    </AppProviders>
  );
  return { engine, ...view };
}

const group = (table: HTMLElement, name: string) => within(table).getByText(name).closest('tbody')!;

describe('Gestão · portfólio', () => {
  it('mostra um grupo por projeto, com os mesmos cabeçalhos e os tipos em linhas separadas', async () => {
    const { engine, container } = renderPortfolio();

    const table = await screen.findByRole('table', { name: /Projetos/ });
    expect(
      within(table)
        .getAllByRole('columnheader')
        .map((header) => header.textContent)
    ).toEqual(['Tipo', 'Abertos', 'Em andamento', 'Entregues em 8 semanas', 'Prazo vencido']);

    const lojaGroup = group(table, 'loja');
    const issues = within(lojaGroup).getByRole('row', { name: /^Issues/ });
    expect(
      within(issues)
        .getAllByRole('cell')
        .map((cell) => cell.textContent)
    ).toEqual(['1', '1', '6', 'sem dado']);
    const pulls = within(lojaGroup).getByRole('row', { name: /^Pull requests/ });
    expect(
      within(pulls)
        .getAllByRole('cell')
        .map((cell) => cell.textContent)
    ).toEqual(['0', '0', '1', 'sem dado']);
    expect(within(lojaGroup).getByText('Sem pontos de atenção')).toBeInTheDocument();
    expect(lojaGroup).toHaveTextContent('1 fonte');

    // Só leitura: a tela não pergunta nada ao modelo.
    expect(engine.calls.map((call) => call.method)).not.toContain('management.portfolioask');
    expect(engine.calls.map((call) => call.method)).toContain('management.portfolio');

    await expectNoA11yViolations(container);
  });

  it('projeto sem ferramenta não vira ponto de atenção: diz "Sem ferramenta" e leva a vincular', async () => {
    renderPortfolio();

    const table = await screen.findByRole('table', { name: /Projetos/ });
    const portal = group(table, 'portal');
    expect(within(portal).getByText('Sem ferramenta')).toBeInTheDocument();
    expect(within(portal).getByRole('link', { name: 'Vincular uma ferramenta' })).toHaveAttribute(
      'href',
      '/integracoes'
    );
    expect(within(portal).queryByRole('list', { name: 'Pontos de atenção de portal' })).not.toBeInTheDocument();
    expect(portal).not.toHaveTextContent('Nenhuma ferramenta vinculada');
    // Sem dado nenhum, não há linha de tipo: nada de zeros que pareçam medida.
    expect(within(portal).queryAllByRole('cell')).toHaveLength(1);
    // Quem tem dado e não pede atenção vem antes de quem está sem ferramenta.
    expect(group(table, 'loja').compareDocumentPosition(portal) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
  });

  it('sem nenhuma linha de dado, os cabeçalhos de coluna ficam só para o leitor de tela', async () => {
    const withoutTool = portfolioFixture.projects.find((project) => project.displayName === 'portal')!;
    renderPortfolio(undefined, {}, { ...portfolioFixture, projects: [withoutTool] });

    const table = await screen.findByRole('table', { name: /Projetos/ });
    expect(within(table).getAllByRole('columnheader')[0]!.closest('thead')).toHaveClass('visually-hidden');
  });

  it('lista os pontos de atenção do projeto e o prazo vencido do tipo que o informa', async () => {
    const withTool = portfolioFixture.projects.find((project) => project.displayName === 'loja')!;
    const withoutTool = portfolioFixture.projects.find((project) => project.displayName === 'portal')!;
    const withSignals = {
      ...portfolioFixture,
      projects: [
        {
          ...withTool,
          kinds: [{ ...withTool.kinds[0]!, overdue: 2 }],
          signals: [{ id: 'overdue', message: '2 itens abertos com prazo vencido.' }]
        },
        withoutTool
      ]
    };
    renderPortfolio(undefined, {}, withSignals);

    const table = await screen.findByRole('table', { name: /Projetos/ });
    const lojaGroup = group(table, 'loja');
    expect(within(lojaGroup).getByRole('list', { name: 'Pontos de atenção de loja' })).toHaveTextContent(
      '2 itens abertos com prazo vencido.'
    );
    expect(within(lojaGroup).queryByText('Sem pontos de atenção')).not.toBeInTheDocument();
    const issues = within(lojaGroup).getByRole('row', { name: /^Issues/ });
    expect(within(issues).getAllByRole('cell')[3]).toHaveTextContent('2');
  });

  it('um projeto que está entre os recentes abre pelo grupo e leva ao acompanhamento', async () => {
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

  it('só oferece abrir o que casa com um recente e explica, no próprio grupo, o que fazer com o resto', async () => {
    renderPortfolio({ pick: vi.fn(), recents: vi.fn().mockResolvedValue([recent('r-loja', 'loja')]) });

    const table = await screen.findByRole('table', { name: /Projetos/ });
    await screen.findByRole('button', { name: 'Abrir projeto loja' });
    expect(screen.queryByRole('button', { name: 'Abrir projeto portal' })).not.toBeInTheDocument();
    expect(group(table, 'portal')).toHaveTextContent('Fora dos recentes: abra a pasta dele pelo seletor do topo.');
    expect(group(table, 'loja')).not.toHaveTextContent('Fora dos recentes');
  });

  it('o projeto aberto agora vem marcado e leva direto ao acompanhamento', async () => {
    renderPortfolio({ pick: vi.fn().mockResolvedValue(loja) });
    await screen.findByRole('table', { name: /Projetos/ });

    await userEvent.click(screen.getByRole('button', { name: 'escolher' }));

    const link = await screen.findByRole('link', { name: 'Ver acompanhamento de loja' });
    expect(link).toHaveAttribute('href', '/gestao');
    expect(screen.getByText('Aberto agora')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Abrir projeto loja' })).not.toBeInTheDocument();
  });

  it('sem nenhum projeto aberto, o botão abre o seletor do topo, que já tem os recentes', async () => {
    renderPortfolio(undefined, {}, { ...portfolioFixture, projects: [] });

    expect(await screen.findByText(/Nenhum projeto aberto neste computador ainda/)).toBeInTheDocument();
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
    expect(screen.getByTestId('switcher')).toHaveTextContent('false');

    await userEvent.click(screen.getByRole('button', { name: 'Escolher um projeto' }));

    expect(screen.getByTestId('switcher')).toHaveTextContent('true');
  });

  it('a pergunta entre projetos fica no Assistente, e a tela leva até lá', async () => {
    renderPortfolio();

    await screen.findByRole('table', { name: /Projetos/ });
    expect(screen.queryByRole('form')).not.toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Assistente' })).toHaveAttribute('href', '/gestao/assistente');
  });
});
