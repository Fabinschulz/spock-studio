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
import { EngineError } from '@/shared/api';
import portfolioAnswerFixture from '@contracts/fixtures/management/portfolio-answer.json';
import portfolioEvidenceFixture from '@contracts/fixtures/management/portfolio-evidence.json';
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
    // Só leituras: ler o portfólio e preparar a pergunta (o estado do modelo e o pacote). Nada é perguntado sem o clique.
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

  describe('perguntar sobre o portfólio', () => {
    const ask = () => screen.findByRole('form', { name: 'Pergunta ao portfólio' });

    it('oferece os projetos, todos marcados, e mostra o que vai ao provedor antes de perguntar', async () => {
      const { engine } = renderPortfolio();

      const form = await ask();

      const group = within(form).getByRole('group', { name: 'Projetos que entram na pergunta' });
      expect(within(group).getByRole('checkbox', { name: 'loja' })).toBeChecked();
      expect(within(group).getByRole('checkbox', { name: 'portal' })).toBeChecked();
      expect(
        await screen.findByText(/O que será enviado ao provedor de modelo: a pergunta e \d+ fatos de 2 projetos/)
      ).toBeInTheDocument();
      expect(engine.calls.some((call) => call.method === 'management.portfolioask')).toBe(false);
    });

    it('desmarcar um projeto pede o pacote só dos que ficaram, e sem nenhum não dá para perguntar', async () => {
      const { engine } = renderPortfolio();
      const form = await ask();
      await userEvent.type(within(form).getByLabelText('Sua pergunta sobre o portfólio'), 'Qual está mais atrasado?');
      await screen.findByText(/O que será enviado ao provedor de modelo/);

      await userEvent.click(within(form).getByRole('checkbox', { name: 'portal' }));
      await waitFor(() =>
        expect(engine.calls.filter((call) => call.method === 'management.portfolioevidence').at(-1)?.params).toEqual({
          projectIds: ['p-000000000001']
        })
      );
      await userEvent.click(within(form).getByRole('checkbox', { name: 'loja' }));

      expect(within(form).getByRole('button', { name: 'Perguntar ao portfólio' })).toBeDisabled();
    });

    it('pergunta com os projetos escolhidos e mostra a resposta conferida e quem é cada projeto', async () => {
      const { engine, container } = renderPortfolio();
      const form = await ask();
      await userEvent.type(within(form).getByLabelText('Sua pergunta sobre o portfólio'), 'Qual está mais atrasado?');

      await userEvent.click(within(form).getByRole('button', { name: 'Perguntar ao portfólio' }));

      const answer = (await screen.findByRole('heading', { name: 'Resposta' })).closest('section')!;
      expect(within(answer).getByText(portfolioAnswerFixture.answer.summary)).toBeInTheDocument();
      expect(screen.getByText('Projetos nesta resposta: p1 = Loja, p2 = Painel.')).toBeInTheDocument();
      const params = engine.calls.find((call) => call.method === 'management.portfolioask')?.params as {
        question: string;
        projectIds: string[];
      };
      expect(params.question).toBe('Qual está mais atrasado?');
      expect([...params.projectIds].sort()).toEqual(['p-000000000001', 'p-000000000002']);
      expect(portfolioEvidenceFixture.projects).toHaveLength(2);

      await expectNoA11yViolations(container);
    });

    it('sem chave de modelo avisa, aponta para Configurações e não deixa perguntar', async () => {
      renderPortfolio(undefined, { modelConfigured: false });
      const form = await ask();

      expect(await screen.findByText('Nenhuma chave de modelo configurada')).toBeInTheDocument();
      await userEvent.type(within(form).getByLabelText('Sua pergunta sobre o portfólio'), 'Qual está mais atrasado?');
      expect(within(form).getByRole('button', { name: 'Perguntar ao portfólio' })).toBeDisabled();
    });

    it('erro do motor aparece com o motivo e nenhuma resposta fica na tela', async () => {
      const { engine } = renderPortfolio();
      const form = await ask();
      const original = engine.client.request.bind(engine.client);
      engine.client.request = ((method: string, ...rest: unknown[]) =>
        method === 'management.portfolioask'
          ? Promise.reject(new EngineError('MODEL_NOT_CONFIGURED', 'Nenhum modelo está configurado.'))
          : (original as (...args: unknown[]) => unknown)(method, ...rest)) as typeof engine.client.request;
      await userEvent.type(within(form).getByLabelText('Sua pergunta sobre o portfólio'), 'Qual está mais atrasado?');

      await userEvent.click(within(form).getByRole('button', { name: 'Perguntar ao portfólio' }));

      expect(await screen.findByRole('alert')).toHaveTextContent('Nenhum modelo está configurado.');
      expect(screen.queryByRole('heading', { name: 'Resposta' })).not.toBeInTheDocument();
    });
  });
});
