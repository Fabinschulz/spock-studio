import { render, screen, within } from '@testing-library/react';
import { MemoryRouter } from 'react-router';
import { describe, expect, it } from 'vitest';

import type { SelectedProject } from '@/features/project-selection';
import type { EngineClient } from '@/shared/api';
import { AppProviders } from '@/shared/components';
import { expectNoA11yViolations } from '@/test/a11y';
import { fakeIntegrationEngine, syncedIntegration } from '@/test/fake-integration-engine';

import { summarizeActivity } from './domain/activity-summary';
import { ManagementPage } from './management-page';

const loja: SelectedProject = { projectId: 'p-aaaaaaaaaaaa', displayName: 'Loja', displayPath: '~\\Loja' };

const renderPage = (client: EngineClient, project: SelectedProject | null) =>
  render(
    <AppProviders>
      <MemoryRouter>
        <ManagementPage client={client} project={project} />
      </MemoryRouter>
    </AppProviders>
  );

describe('Gestão · acompanhamento do projeto', () => {
  it('sem projeto aberto não pede nada ao motor', () => {
    const engine = fakeIntegrationEngine();
    renderPage(engine.client, null);

    expect(screen.getByText(/Nenhum projeto selecionado/)).toBeInTheDocument();
    expect(engine.calls).toEqual([]);
  });

  it('sem fonte vinculada diz que não há dados, em vez de mostrar zeros', async () => {
    const engine = fakeIntegrationEngine();
    renderPage(engine.client, loja);

    expect(await screen.findByText(/Não há dados de atividades: nenhuma ferramenta foi vinculada/)).toBeInTheDocument();
    expect(screen.queryByText('A fazer')).not.toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Fontes de dados' })).not.toBeInTheDocument();
  });

  it('fonte vinculada e ainda não sincronizada também é ausência de dado', async () => {
    const engine = fakeIntegrationEngine();
    engine.seed(loja.projectId, {
      ...syncedIntegration,
      lastSync: null,
      items: { total: 0, byKind: {}, byStatusCategory: {}, lastSyncedAt: null }
    });
    renderPage(engine.client, loja);

    expect(await screen.findByText(/ainda não trouxeram nenhum item/)).toBeInTheDocument();
    expect(screen.getByText(/sem itens sincronizados/)).toBeInTheDocument();
  });

  it('mostra as contagens observadas, de onde vieram e o que ainda não é calculado', async () => {
    const engine = fakeIntegrationEngine();
    engine.seed(loja.projectId);
    const { container } = renderPage(engine.client, loja);

    const activity = (await screen.findByRole('heading', { name: 'Atividades' })).closest('section')!;
    expect(within(activity).getByText('A fazer').nextElementSibling).toHaveTextContent('1');
    expect(within(activity).getByText('Concluídas').nextElementSibling).toHaveTextContent('1');
    expect(within(activity).queryByText('Em andamento')).not.toBeInTheDocument();

    const sources = screen.getByRole('heading', { name: 'Fontes de dados' }).closest('section')!;
    expect(within(sources).getByText('acme/app')).toBeInTheDocument();
    expect(within(sources).getByText(/GitHub · Issues: 1, Pull requests: 1 · lido/)).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Ainda não calculado' })).toBeInTheDocument();
    expect(engine.calls.every((call) => /^(integration|management)\./.test(call.method))).toBe(true);

    const flow = screen.getByRole('heading', { name: 'Fluxo nas últimas 4 semanas' }).closest('section')!;
    const issues = within(flow).getByRole('group', { name: 'Issues' });
    expect(within(issues).getByText('Entregues por semana (média)').nextElementSibling).toHaveTextContent('1,5');
    expect(within(issues).getByText('Em andamento').nextElementSibling).toHaveTextContent('1');
    expect(within(flow).getAllByText('A fonte não informa prazo para estes itens.')).toHaveLength(2);
    expect(
      within(flow).getByRole('table', { name: 'Issues: itens entregues e itens novos por semana' })
    ).toBeInTheDocument();
    expect(within(flow).queryByText('Dados incompletos')).not.toBeInTheDocument();

    const missing = screen.getByRole('heading', { name: 'Ainda não calculado' }).closest('section')!;
    expect(within(missing).getByText('Previsão de entrega.')).toBeInTheDocument();

    await expectNoA11yViolations(container);
  });
});

describe('summarizeActivity', () => {
  it('soma as fontes na ordem do fluxo e guarda a leitura mais antiga', () => {
    const other = {
      ...syncedIntegration,
      id: 'pint-000000000002',
      items: {
        total: 4,
        byKind: { issues: 4 },
        byStatusCategory: { inProgress: 3, done: 1, revisao: 2 },
        lastSyncedAt: '2026-11-01T08:00:00+00:00'
      }
    };

    const summary = summarizeActivity([syncedIntegration, other]);

    expect(summary.total).toBe(8);
    expect(summary.byCategory).toEqual([
      { category: 'todo', label: 'A fazer', count: 1 },
      { category: 'inProgress', label: 'Em andamento', count: 3 },
      { category: 'done', label: 'Concluídas', count: 2 },
      { category: 'revisao', label: 'revisao', count: 2 }
    ]);
    expect(summary.oldestReadAt).toBe('2026-11-01T08:00:00+00:00');
  });

  it('sem fontes não há total nem data', () => {
    expect(summarizeActivity([])).toEqual({ total: 0, byCategory: [], oldestReadAt: null });
  });
});
