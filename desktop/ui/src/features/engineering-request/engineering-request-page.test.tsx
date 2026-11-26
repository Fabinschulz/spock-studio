import reviewFixture from '@contracts/fixtures/context/review.json';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { useEffect, type ReactNode } from 'react';
import { MemoryRouter, Route, Routes } from 'react-router';

import { EngineError, type EngineClient } from '@/shared/api';
import { AppProviders } from '@/shared/components';
import { ProjectSelectionProvider, useProjectSelectionContext } from '@/features/project-selection';
import { handoffState } from '@/shared/routing';
import { workflowStartParamsSchema, workflowStartResultSchema } from '@/shared/schema';
import { expectNoA11yViolations } from '@/test/a11y';

import { EngineeringRequestPage } from './engineering-request-page';

const project = { projectId: 'p-aaaaaaaaaaaa', displayName: 'EmpregaNetAPI', displayPath: '~\\EmpregaNetAPI' };

function AutoPick({ children }: { children: ReactNode }) {
  const { pick, selected } = useProjectSelectionContext();
  useEffect(() => {
    if (!selected) void pick();
  }, [pick, selected]);
  return children;
}

type Model = { configured: boolean } | 'erro';

function clientFor(
  runId: string,
  model: Model = { configured: false },
  review: object | Error = reviewFixture
): EngineClient & { calls: Array<{ method: string; params: unknown }> } {
  const calls: Array<{ method: string; params: unknown }> = [];
  return {
    calls,
    async request(method, params, schema) {
      calls.push({ method, params });
      if (method === 'model.status') {
        if (model === 'erro') throw new Error('motor fora do ar');
        return schema.parse({ configured: model.configured, provider: model.configured ? 'openai' : '' });
      }
      if (method === 'context.review') {
        if (review instanceof Error) throw review;
        return schema.parse(review);
      }
      if (method === 'workflow.start') {
        workflowStartParamsSchema.parse(params);
        return schema.parse(workflowStartResultSchema.parse({ runId }));
      }
      throw new Error(method);
    },
    async onEvent() {
      return () => undefined;
    }
  };
}

function renderPage(client: EngineClient, handoff?: string) {
  const entry = handoff ? { pathname: '/pedido', state: handoffState(handoff) } : '/pedido';
  return render(
    <AppProviders>
      <ProjectSelectionProvider picker={{ pick: vi.fn().mockResolvedValue(project) }}>
        <MemoryRouter initialEntries={[entry]}>
          <AutoPick>
            <Routes>
              <Route path="/pedido" element={<EngineeringRequestPage client={client} />} />
              <Route path="/execucao/:runId" element={<div data-testid="exec">Execução</div>} />
            </Routes>
          </AutoPick>
        </MemoryRouter>
      </ProjectSelectionProvider>
    </AppProviders>
  );
}

describe('Pedido de engenharia', () => {
  it('chama workflow.start com contrato e redireciona para a execução', async () => {
    const client = clientFor('run-eng-1');
    renderPage(client);

    const goal = await screen.findByPlaceholderText(/descrever impacto/i);
    await userEvent.type(goal, 'descrever o serviço de autenticação');
    await userEvent.click(screen.getByRole('button', { name: /^Iniciar pedido$/ }));

    expect(await screen.findByTestId('exec')).toBeInTheDocument();
    expect(client.calls.filter((call) => call.method === 'workflow.start')).toEqual([
      {
        method: 'workflow.start',
        params: {
          projectId: 'p-aaaaaaaaaaaa',
          input: { goal: 'descrever o serviço de autenticação', impact: true }
        }
      }
    ]);
  });

  it('quem chega pela entrada de pedidos encontra o objetivo preenchido, e nada começa sem o clique', async () => {
    const client = clientFor('run-eng-2');
    renderPage(client, 'trocar o contrato de autenticação');

    expect(await screen.findByRole('textbox', { name: /Objetivo/ })).toHaveValue('trocar o contrato de autenticação');
    expect(client.calls.some((call) => call.method === 'workflow.start')).toBe(false);

    await userEvent.click(screen.getByRole('button', { name: /^Iniciar pedido$/ }));

    expect(await screen.findByTestId('exec')).toBeInTheDocument();
  });

  it('sem modelo configurado a opção fica desligada e leva às Configurações', async () => {
    const client = clientFor('run-eng-2', { configured: false });
    renderPage(client);

    const option = await screen.findByRole('checkbox', { name: /Usar o modelo de IA/ });
    expect(await screen.findByText(/Nenhum modelo está configurado/)).toBeInTheDocument();
    expect(option).toBeDisabled();
    expect(option).not.toBeChecked();
    expect(screen.getByRole('button', { name: 'Salvar a chave em Configurações' })).toBeInTheDocument();
  });

  it('se o motor não responde sobre o modelo, a opção também não é oferecida', async () => {
    const client = clientFor('run-eng-3', 'erro');
    renderPage(client);

    const option = await screen.findByRole('checkbox', { name: /Usar o modelo de IA/ });
    expect(await screen.findByText(/Nenhum modelo está configurado/)).toBeInTheDocument();
    expect(option).toBeDisabled();
  });

  it('com modelo configurado a opção começa desmarcada, avisa o que sai do computador e só então vai no pedido', async () => {
    const client = clientFor('run-eng-4', { configured: true });
    renderPage(client);

    const option = await screen.findByRole('checkbox', { name: /Usar o modelo de IA/ });
    await waitFor(() => expect(option).toBeEnabled());
    expect(option).not.toBeChecked();
    expect(option).toHaveAccessibleDescription(
      /enviados ao provedor do modelo configurado em Conexões › Modelos de IA.*8\.000 tokens.*Sem marcar, nada sai do seu computador/
    );
    expect(screen.queryByText(/Nenhum modelo está configurado/)).not.toBeInTheDocument();

    await userEvent.type(screen.getByPlaceholderText(/descrever impacto/i), 'avaliar o módulo de cobrança');
    await userEvent.click(option);
    await userEvent.click(screen.getByRole('button', { name: /^Iniciar pedido$/ }));

    expect(await screen.findByTestId('exec')).toBeInTheDocument();
    const start = client.calls.find((call) => call.method === 'workflow.start');
    expect(start?.params).toEqual({
      projectId: 'p-aaaaaaaaaaaa',
      input: { goal: 'avaliar o módulo de cobrança', impact: true, useModel: true }
    });
  });

  it('revisa o contexto antes de iniciar: tokens por etapa, orçamento e o que ficou de fora', async () => {
    const client = clientFor('run-eng-5');
    const { container } = renderPage(client);

    await userEvent.type(await screen.findByPlaceholderText(/descrever impacto/i), '  Corrigir o frete  ');
    await userEvent.click(screen.getByRole('button', { name: 'Revisar contexto' }));

    const panel = await screen.findByRole('region', { name: 'Contexto do pedido' });
    expect(client.calls.find((call) => call.method === 'context.review')?.params).toEqual({
      projectId: 'p-aaaaaaaaaaaa',
      goal: 'Corrigir o frete'
    });
    expect(within(panel).getByText('Cabe no orçamento')).toBeInTheDocument();
    expect(panel).toHaveTextContent('No pior caso, a execução usa 2.850 de 8.000 tokens');
    expect(within(panel).getByText(/Nenhum modelo configurado/)).toBeInTheDocument();
    expect(panel).toHaveTextContent('Planejamento: 411 de entrada e até 1.024 de resposta');
    expect(panel).toHaveTextContent('Trechos do projeto: 200');
    expect(panel).toHaveTextContent('src/frete.cs · 200 tokens');
    expect(panel).toHaveTextContent('Não couberam no bloco de trechos: src/carrinho.cs');
    expect(panel).toHaveTextContent('1 arquivo ainda não foi indexado');
    expect(client.calls.some((call) => call.method === 'workflow.start')).toBe(false);
    await expectNoA11yViolations(container);
  });

  it('avisa quando uma etapa passa do orçamento e conta exato com modelo da OpenAI', async () => {
    const over = {
      ...reviewFixture,
      model: { configured: true, provider: 'openai', model: 'gpt-4o-mini' },
      counting: { exact: true, method: 'Contagem exata com o tokenizador do gpt-4o-mini.' },
      plannedTokens: 9100,
      calls: reviewFixture.calls.map((call, index) => ({ ...call, withinBudget: index === 0 }))
    };
    renderPage(clientFor('run-eng-6', { configured: true }, over));

    await userEvent.type(await screen.findByPlaceholderText(/descrever impacto/i), 'frete');
    await userEvent.click(screen.getByRole('button', { name: 'Revisar contexto' }));

    const panel = await screen.findByRole('region', { name: 'Contexto do pedido' });
    expect(within(panel).getByText('Passa do orçamento')).toBeInTheDocument();
    expect(panel).toHaveTextContent('Contagem exata para gpt-4o-mini.');
    expect(within(panel).getAllByText(/Esta etapa passa do orçamento/)).toHaveLength(1);
  });

  it('se a revisão falha, diz o motivo sem impedir o pedido', async () => {
    renderPage(
      clientFor(
        'run-eng-7',
        { configured: false },
        new EngineError('AI_RUNTIME_UNAVAILABLE', 'O runtime de IA não está disponível.')
      )
    );

    await userEvent.type(await screen.findByPlaceholderText(/descrever impacto/i), 'frete');
    await userEvent.click(screen.getByRole('button', { name: 'Revisar contexto' }));

    expect(await screen.findByText(/O runtime de IA não está disponível/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /^Iniciar pedido$/ })).toBeEnabled();
  });

  it('pede pasta quando ainda não há projeto autorizado', async () => {
    const client = clientFor('run-x');
    render(
      <AppProviders>
        <ProjectSelectionProvider picker={{ pick: vi.fn().mockResolvedValue(null) }}>
          <MemoryRouter initialEntries={['/pedido']}>
            <EngineeringRequestPage client={client} />
          </MemoryRouter>
        </ProjectSelectionProvider>
      </AppProviders>
    );

    expect(await screen.findByRole('heading', { name: 'Pedido de engenharia' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Selecionar pasta' })).toBeInTheDocument();
    expect(client.calls).toHaveLength(0);
  });
});
