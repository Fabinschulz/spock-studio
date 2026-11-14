import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router';
import { describe, expect, it } from 'vitest';

import type { SelectedProject } from '@/features/project-selection';
import type { EngineClient } from '@/shared/api';
import { AppProviders } from '@/shared/components';
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

describe('Gestão · assistente', () => {
  it('sem projeto aberto não pede nada ao motor', () => {
    const engine = fakeIntegrationEngine();
    renderPage(engine.client, null);

    expect(screen.getByText(/Nenhum projeto selecionado/)).toBeInTheDocument();
    expect(engine.calls).toEqual([]);
  });

  it('sem chave de modelo avisa, aponta para Configurações e não deixa perguntar', async () => {
    const engine = fakeIntegrationEngine({ modelConfigured: false });
    renderPage(engine.client);

    expect(await screen.findByText('Nenhuma chave de modelo configurada')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Configurações' })).toHaveAttribute('href', '/configuracoes');
    await userEvent.type(screen.getByLabelText('Sua pergunta'), 'Qual o risco?');
    expect(screen.getByRole('button', { name: 'Perguntar' })).toBeDisabled();
  });

  it('mostra o que será enviado, pergunta e exibe a resposta separada, com os fatos citados', async () => {
    const engine = fakeIntegrationEngine();
    const { container } = renderPage(engine.client);

    expect(
      await screen.findByText(/O que será enviado ao provedor de modelo: a pergunta e \d+ fatos/)
    ).toBeInTheDocument();
    expect(engine.calls.some((call) => call.method === 'management.ask')).toBe(false);

    await userEvent.click(screen.getByRole('button', { name: 'Quais são os principais riscos agora?' }));
    expect(screen.getByLabelText('Sua pergunta')).toHaveValue('Quais são os principais riscos agora?');
    await userEvent.click(screen.getByRole('button', { name: 'Perguntar' }));

    const heading = await screen.findByRole('heading', { name: 'Resposta' });
    expect(heading).toHaveFocus();
    const answer = heading.closest('section')!;
    expect(within(answer).getByText('Há 1 item vencido e a média é de 2 entregas por semana.')).toBeInTheDocument();
    const observed = within(answer).getByRole('group', { name: /Dados observados/ });
    expect(observed).toHaveTextContent('Há 1 item com prazo vencido.');
    expect(within(observed).getByText('1 fato citado')).toBeInTheDocument();
    expect(within(answer).getByRole('group', { name: /Inferências/ })).toHaveTextContent('não são fatos');
    expect(within(answer).getByText('Informações indisponíveis')).toBeInTheDocument();
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

  it('"Investigar em etapas" chama a investigação, mostra a resposta conferida e o caminho, com o link da central', async () => {
    const engine = fakeIntegrationEngine();
    const { container } = renderPage(engine.client);

    await userEvent.type(await screen.findByLabelText('Sua pergunta'), 'Por que a vazão caiu?');
    expect(screen.getByRole('button', { name: 'Perguntar' })).toBeEnabled();
    await userEvent.click(screen.getByRole('checkbox', { name: 'Investigar em etapas' }));
    await userEvent.click(screen.getByRole('button', { name: 'Investigar' }));

    const answer = (await screen.findByRole('heading', { name: 'Resposta' })).closest('section')!;
    expect(within(answer).getByText('Há 2 itens abertos.')).toBeInTheDocument();
    expect(answer).toHaveTextContent('Não cita nenhum fato consultado na investigação.');
    expect(engine.calls.some((call) => call.method === 'management.ask')).toBe(false);
    expect(engine.calls.find((call) => call.method === 'management.investigate')?.params).toEqual({
      projectId: loja.projectId,
      question: 'Por que a vazão caiu?'
    });

    const path = screen.getByRole('region', { name: 'Caminho da investigação' });
    expect(path).toHaveTextContent('Caminho da investigação: 1 rodada, 3 etapas');
    expect(path).toHaveTextContent('Pergunta enquadrada');
    expect(path).toHaveTextContent('Fatos: today, flow.issues.items, risk.overdue.');
    expect(within(path).getByRole('list', { name: 'O que limitou a investigação' })).toHaveTextContent(
      'A investigação parou antes de olhar: riscos.'
    );
    expect(within(path).getByRole('link', { name: 'central de execuções' })).toHaveAttribute('href', '/execucoes');

    await expectNoA11yViolations(container);
  });

  it('a investigação roda sem chave de modelo, e perguntar normalmente continua travado', async () => {
    const engine = fakeIntegrationEngine({ modelConfigured: false });
    renderPage(engine.client);

    await userEvent.type(await screen.findByLabelText('Sua pergunta'), 'Quais os riscos?');
    expect(screen.getByRole('button', { name: 'Perguntar' })).toBeDisabled();
    await userEvent.click(screen.getByRole('checkbox', { name: 'Investigar em etapas' }));

    expect(screen.getByRole('button', { name: 'Investigar' })).toBeEnabled();
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
    await waitFor(() => expect(within(alert).getByRole('link', { name: 'Configurações' })).toBeInTheDocument());
    expect(screen.queryByRole('heading', { name: 'Resposta' })).not.toBeInTheDocument();
  });
});
