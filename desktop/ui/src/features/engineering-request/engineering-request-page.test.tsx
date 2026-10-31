import { render, screen } from '@testing-library/react';
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

function clientFor(runId: string): EngineClient & { calls: Array<{ method: string; params: unknown }> } {
  const calls: Array<{ method: string; params: unknown }> = [];
  return {
    calls,
    async request(method, params, schema) {
      calls.push({ method, params });
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
              <Route
                path="/pedido"
                element={<EngineeringRequestPage client={client} />}
              />
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

    expect(await screen.findByLabelText('Objetivo')).toBeInTheDocument();
    await userEvent.type(screen.getByLabelText('Objetivo'), 'descrever o serviço de autenticação');
    await userEvent.click(screen.getByRole('button', { name: 'Iniciar pedido' }));

    expect(await screen.findByTestId('exec')).toBeInTheDocument();
    expect(client.calls).toEqual([
      {
        method: 'workflow.start',
        params: {
          projectId: 'p-aaaaaaaaaaaa',
          input: { goal: 'descrever o serviço de autenticação', impact: true }
        }
      }
    ]);
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
    expect(screen.getByRole('button', { name: 'Selecionar pasta do projeto' })).toBeInTheDocument();
    expect(client.calls).toHaveLength(0);
  });
});
