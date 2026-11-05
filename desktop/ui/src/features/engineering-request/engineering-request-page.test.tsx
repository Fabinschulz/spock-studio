import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { useEffect, type ReactNode } from 'react';
import { MemoryRouter, Route, Routes } from 'react-router';

import type { EngineClient } from '@/shared/api';
import { AppProviders } from '@/shared/components';
import { ProjectSelectionProvider, useProjectSelectionContext } from '@/features/project-selection';
import { workflowStartParamsSchema, workflowStartResultSchema } from '@/shared/schema';

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
  model: Model = { configured: false }
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

function renderPage(client: EngineClient) {
  return render(
    <AppProviders>
      <ProjectSelectionProvider picker={{ pick: vi.fn().mockResolvedValue(project) }}>
        <MemoryRouter initialEntries={['/pedido']}>
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

  it('sem modelo configurado a opção fica desligada e leva às Configurações', async () => {
    const client = clientFor('run-eng-2', { configured: false });
    renderPage(client);

    const option = await screen.findByRole('checkbox', { name: /Usar o modelo de IA/ });
    expect(await screen.findByText(/Nenhum modelo está configurado/)).toBeInTheDocument();
    expect(option).toBeDisabled();
    expect(option).not.toBeChecked();
    expect(screen.getByRole('button', { name: 'Guardar a chave em Configurações' })).toBeInTheDocument();
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
      /enviados à OpenAI.*8\.000 tokens.*Sem marcar, nada sai do seu computador/
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
