import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router';
import { describe, expect, it } from 'vitest';

import type { SelectedProject } from '@/features/project-selection';
import { EngineError, type EngineClient } from '@/shared/api';
import { AppProviders } from '@/shared/components';
import portfolioAnswerFixture from '@contracts/fixtures/management/portfolio-answer.json';
import portfolioFixture from '@contracts/fixtures/management/portfolio.json';
import { expectNoA11yViolations } from '@/test/a11y';
import { fakeIntegrationEngine } from '@/test/fake-integration-engine';

import { AssistantPage } from './assistant-page';

const loja: SelectedProject = { projectId: 'p-aaaaaaaaaaaa', displayName: 'Loja', displayPath: '~\\Loja' };

const renderPage = (client: EngineClient, project: SelectedProject | null = loja) =>
  render(
    <AppProviders>
      <MemoryRouter>
        <AssistantPage client={client} project={project} />
      </MemoryRouter>
    </AppProviders>
  );

/** Troca a resposta de um método do motor de teste, mantendo os outros. */
function replace(client: EngineClient, method: string, reply: () => Promise<unknown>) {
  const original = client.request.bind(client);
  client.request = ((name: string, ...rest: unknown[]) =>
    name === method ? reply() : (original as (...args: unknown[]) => unknown)(name, ...rest)) as typeof client.request;
}

const scope = () => screen.findByRole('group', { name: 'Sobre o que perguntar' });
const mode = () => screen.getByRole('group', { name: 'Como responder' });

describe('Gestão · assistente sobre o projeto', () => {
  it('sem chave de modelo, a resposta direta fica indisponível com o motivo e a investigação vem marcada', async () => {
    const engine = fakeIntegrationEngine({ modelConfigured: false });
    renderPage(engine.client);

    expect(await screen.findByText('Nenhuma chave de modelo configurada')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Configurações › Minha IA' })).toHaveAttribute('href', '/configuracoes/ia');
    const direct = within(mode()).getByRole('radio', { name: 'Resposta direta' });
    expect(direct).toBeDisabled();
    expect(direct).toHaveAccessibleDescription('Precisa de uma chave de modelo, e nenhuma está configurada.');
    expect(within(mode()).getByRole('radio', { name: 'Investigação em etapas' })).toBeChecked();

    await userEvent.type(screen.getByLabelText('Sua pergunta'), 'Quais os riscos?');
    expect(screen.getByRole('button', { name: 'Investigar' })).toBeEnabled();
    expect(await screen.findByText(/Nada é enviado/)).toBeInTheDocument();
  });

  it('a sugestão vem antes do campo, só o preenche e leva o foco a ele', async () => {
    const engine = fakeIntegrationEngine();
    renderPage(engine.client);

    const suggestion = await screen.findByRole('button', { name: 'Quais são os principais riscos agora?' });
    const field = screen.getByLabelText('Sua pergunta');
    expect(suggestion.compareDocumentPosition(field) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();

    await userEvent.click(suggestion);

    expect(field).toHaveValue('Quais são os principais riscos agora?');
    expect(field).toHaveFocus();
    expect(engine.calls.some((call) => call.method === 'management.ask')).toBe(false);
  });

  it('com chave, pergunta direto e mostra a resposta separada, com os fatos citados', async () => {
    const engine = fakeIntegrationEngine();
    const { container } = renderPage(engine.client);

    expect(await scope()).toBeInTheDocument();
    expect(within(await scope()).getByRole('radio', { name: 'Este projeto (Loja)' })).toBeChecked();
    expect(within(mode()).getByRole('radio', { name: 'Resposta direta' })).toBeChecked();
    expect(
      await screen.findByText(/O que será enviado ao provedor de modelo: a pergunta e \d+ fatos/)
    ).toBeInTheDocument();

    await userEvent.type(screen.getByLabelText('Sua pergunta'), 'Quais são os principais riscos agora?');
    await userEvent.click(screen.getByRole('button', { name: 'Perguntar' }));

    const heading = await screen.findByRole('heading', { name: 'Resposta' });
    expect(heading).toHaveFocus();
    const answer = heading.closest('section')!;
    expect(within(answer).getByText('Há 1 item vencido e a média é de 2 entregas por semana.')).toBeInTheDocument();
    const observed = within(answer).getByRole('group', { name: /Dados observados/ });
    expect(observed).toHaveTextContent('Há 1 item com prazo vencido.');
    expect(within(observed).getByText('1 fato citado')).toBeInTheDocument();
    expect(within(answer).getByRole('group', { name: /Inferências/ })).toHaveTextContent('não são fatos');
    expect(within(answer).queryByText(/descartada/)).not.toBeInTheDocument();
    expect(engine.calls.find((call) => call.method === 'management.ask')?.params).toEqual({
      projectId: loja.projectId,
      question: 'Quais são os principais riscos agora?'
    });

    await expectNoA11yViolations(container);
  });

  it('mostra o que a verificação descartou, com o motivo', async () => {
    const engine = fakeIntegrationEngine({ assistant: 'rejected' });
    renderPage(engine.client);

    await userEvent.type(await screen.findByLabelText('Sua pergunta'), 'Qual a média?');
    await userEvent.click(screen.getByRole('button', { name: 'Perguntar' }));

    const answer = (await screen.findByRole('heading', { name: 'Resposta' })).closest('section')!;
    expect(within(answer).getByText('1 afirmação descartada pela verificação')).toBeInTheDocument();
    expect(answer).toHaveTextContent('Traz o número 3.5, que não está nos fatos citados.');
  });

  it('a investigação em etapas mostra a resposta conferida e o caminho, com os fatos por texto', async () => {
    const engine = fakeIntegrationEngine();
    const { container } = renderPage(engine.client);

    await userEvent.type(await screen.findByLabelText('Sua pergunta'), 'Por que a vazão caiu?');
    await userEvent.click(within(mode()).getByRole('radio', { name: 'Investigação em etapas' }));
    expect(screen.getByText(/O que pode ir ao provedor de modelo/)).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Investigar' }));

    const answer = (await screen.findByRole('heading', { name: 'Resposta' })).closest('section')!;
    expect(within(answer).getByText('Há 2 itens abertos.')).toBeInTheDocument();
    expect(engine.calls.some((call) => call.method === 'management.ask')).toBe(false);
    expect(engine.calls.find((call) => call.method === 'management.investigate')?.params).toEqual({
      projectId: loja.projectId,
      question: 'Por que a vazão caiu?'
    });

    const path = screen.getByRole('region', { name: 'Caminho da investigação' });
    expect(path).toHaveTextContent('Caminho da investigação: 1 rodada, 3 etapas');
    const facts = within(path).getByRole('list', { name: 'Fatos usados nesta etapa' });
    expect(facts).toHaveTextContent('Issues: 18 itens sincronizados, 2 abertos.');
    expect(facts).toHaveTextContent(/Hoje é/);
    expect(path).not.toHaveTextContent('flow.issues.items');
    expect(within(path).getByRole('link', { name: 'Execuções' })).toHaveAttribute('href', '/execucoes');

    await expectNoA11yViolations(container);
  });

  it('o botão diz o que falta na pergunta', async () => {
    const engine = fakeIntegrationEngine();
    renderPage(engine.client);

    const button = await screen.findByRole('button', { name: 'Perguntar' });
    expect(button).toHaveAccessibleDescription('Escreva a pergunta, de 3 a 500 caracteres.');
    await userEvent.type(screen.getByLabelText('Sua pergunta'), 'Qual o risco?');
    expect(button).toBeEnabled();
    expect(button).not.toHaveAccessibleDescription();
  });

  it('erro do modelo aparece com o motivo, e falta de chave leva a Configurações', async () => {
    const engine = fakeIntegrationEngine({
      assistant: { code: 'MODEL_NOT_CONFIGURED', message: 'Nenhum modelo está configurado.' }
    });
    renderPage(engine.client);

    await userEvent.type(await screen.findByLabelText('Sua pergunta'), 'Qual o risco?');
    await userEvent.click(screen.getByRole('button', { name: 'Perguntar' }));

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('Nenhum modelo está configurado.');
    await waitFor(() =>
      expect(within(alert).getByRole('link', { name: 'Configurações › Minha IA' })).toBeInTheDocument()
    );
    expect(screen.queryByRole('heading', { name: 'Resposta' })).not.toBeInTheDocument();
  });

  it('a pergunta que chega digitada fica ao trocar de escopo', async () => {
    const engine = fakeIntegrationEngine();
    renderPage(engine.client);

    await userEvent.type(await screen.findByLabelText('Sua pergunta'), 'Qual está mais atrasado?');
    await userEvent.click(within(await scope()).getByRole('radio', { name: 'Vários projetos' }));

    expect(screen.getByLabelText('Sua pergunta sobre os projetos')).toHaveValue('Qual está mais atrasado?');
  });
});

describe('Gestão · assistente sobre vários projetos', () => {
  const ask = async (client: EngineClient, project: SelectedProject | null = loja) => {
    renderPage(client, project);
    if (project) await userEvent.click(within(await scope()).getByRole('radio', { name: 'Vários projetos' }));
    return screen.findByRole('form', { name: 'Pergunta a vários projetos' });
  };

  it('sem projeto aberto, já começa em vários projetos, e "Este projeto" diz por que não está disponível', async () => {
    const engine = fakeIntegrationEngine();
    await ask(engine.client, null);

    const project = within(await scope()).getByRole('radio', { name: 'Este projeto' });
    expect(project).toBeDisabled();
    expect(project).toHaveAccessibleDescription('Nenhum projeto aberto: escolha um no seletor do topo.');
    expect(within(await scope()).getByRole('radio', { name: 'Vários projetos' })).toBeChecked();
  });

  it('sem projeto aberto e com menos de dois projetos, não há o que perguntar', async () => {
    const engine = fakeIntegrationEngine();
    replace(engine.client, 'management.portfolio', async () => ({
      ...portfolioFixture,
      projects: portfolioFixture.projects.slice(0, 1)
    }));
    renderPage(engine.client, null);

    expect(await screen.findByText(/Nenhum projeto selecionado/)).toBeInTheDocument();
  });

  it('oferece os projetos, todos marcados, e mostra o que vai ao provedor, com os fatos comuns contados', async () => {
    const engine = fakeIntegrationEngine();
    const form = await ask(engine.client);

    const group = within(form).getByRole('group', { name: 'Projetos que entram na pergunta' });
    expect(within(group).getByRole('checkbox', { name: 'loja' })).toBeChecked();
    expect(within(group).getByRole('checkbox', { name: 'portal' })).toBeChecked();
    const summary = await screen.findByText(
      /O que será enviado ao provedor de modelo: a pergunta e \d+ fatos de 2 projetos/
    );
    expect(summary.closest('details')).toHaveTextContent(/fato comum a todos os projetos/);
    expect(summary.closest('details')).not.toHaveTextContent(/\bp1\b/);
    expect(engine.calls.some((call) => call.method === 'management.portfolioask')).toBe(false);
  });

  it('desmarcar um projeto pede o pacote só dos que ficaram, e sem nenhum o botão diz o que falta', async () => {
    const engine = fakeIntegrationEngine();
    const form = await ask(engine.client);
    await userEvent.type(within(form).getByLabelText('Sua pergunta sobre os projetos'), 'Qual está mais atrasado?');
    await screen.findByText(/O que será enviado ao provedor de modelo/);

    await userEvent.click(within(form).getByRole('checkbox', { name: 'portal' }));
    await waitFor(() =>
      expect(engine.calls.filter((call) => call.method === 'management.portfolioevidence').at(-1)?.params).toEqual({
        projectIds: ['p-000000000001']
      })
    );
    await userEvent.click(within(form).getByRole('checkbox', { name: 'loja' }));

    const button = within(form).getByRole('button', { name: 'Perguntar aos projetos' });
    expect(button).toBeDisabled();
    expect(button).toHaveAccessibleDescription('Escolha pelo menos um projeto.');
  });

  it('pergunta com os projetos escolhidos e mostra a resposta conferida e quem é cada projeto', async () => {
    const engine = fakeIntegrationEngine();
    const form = await ask(engine.client);
    await userEvent.type(within(form).getByLabelText('Sua pergunta sobre os projetos'), 'Qual está mais atrasado?');

    await userEvent.click(within(form).getByRole('button', { name: 'Perguntar aos projetos' }));

    const answer = (await screen.findByRole('heading', { name: 'Resposta' })).closest('section')!;
    expect(within(answer).getByText(portfolioAnswerFixture.answer.summary)).toBeInTheDocument();
    expect(screen.getByText('Projetos nesta resposta: p1 = Loja, p2 = Painel.')).toBeInTheDocument();
    const params = engine.calls.find((call) => call.method === 'management.portfolioask')?.params as {
      question: string;
      projectIds: string[];
    };
    expect(params.question).toBe('Qual está mais atrasado?');
    expect([...params.projectIds].sort()).toEqual(['p-000000000001', 'p-000000000002']);
  });

  it('sem chave de modelo, perguntar a vários projetos fica travado com o motivo', async () => {
    const engine = fakeIntegrationEngine({ modelConfigured: false });
    const form = await ask(engine.client);

    await userEvent.type(within(form).getByLabelText('Sua pergunta sobre os projetos'), 'Qual está mais atrasado?');
    const button = within(form).getByRole('button', { name: 'Perguntar aos projetos' });
    expect(button).toBeDisabled();
    expect(button).toHaveAccessibleDescription('Para perguntar a vários projetos, configure uma chave de modelo.');
  });

  it('erro do motor aparece com o motivo e nenhuma resposta fica na tela', async () => {
    const engine = fakeIntegrationEngine();
    replace(engine.client, 'management.portfolioask', () =>
      Promise.reject(new EngineError('MODEL_NOT_CONFIGURED', 'Nenhum modelo está configurado.'))
    );
    const form = await ask(engine.client);
    await userEvent.type(within(form).getByLabelText('Sua pergunta sobre os projetos'), 'Qual está mais atrasado?');

    await userEvent.click(within(form).getByRole('button', { name: 'Perguntar aos projetos' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('Nenhum modelo está configurado.');
    expect(screen.queryByRole('heading', { name: 'Resposta' })).not.toBeInTheDocument();
  });
});
