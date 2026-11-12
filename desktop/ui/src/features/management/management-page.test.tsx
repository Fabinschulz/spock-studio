import { render, screen, within } from '@testing-library/react';
import { MemoryRouter } from 'react-router';
import { describe, expect, it } from 'vitest';

import type { SelectedProject } from '@/features/project-selection';
import type { EngineClient } from '@/shared/api';
import { AppProviders } from '@/shared/components';
import { expectNoA11yViolations } from '@/test/a11y';
import linkedFixture from '@contracts/fixtures/runs/run-linked.json';
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
    expect(screen.queryByRole('heading', { name: 'Previsão de entrega' })).not.toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Riscos' })).not.toBeInTheDocument();
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
    expect(engine.calls.every((call) => /^(integration|management|run)\./.test(call.method))).toBe(true);

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
    expect(within(missing).getByText('Cycle time.')).toBeInTheDocument();
    expect(within(missing).queryByText('Previsão de entrega.')).not.toBeInTheDocument();

    const forecast = screen.getByRole('heading', { name: 'Previsão de entrega' }).closest('section')!;
    const issueForecast = within(forecast).getByRole('group', { name: 'Issues: 10 abertos' });
    expect(within(issueForecast).getByText('Confiança alta')).toBeInTheDocument();
    expect(within(issueForecast).getByText('Provável').closest('div')).toHaveTextContent('15/12/2026');
    expect(within(issueForecast).getByText('Conservador').closest('div')).toHaveTextContent('29/12/2026');
    expect(within(issueForecast).getByText(/85% das simulações terminam até esta data/)).toBeInTheDocument();
    expect(within(forecast).getByRole('group', { name: 'Pull requests: 1 aberto' })).toHaveTextContent(
      'Sem previsão. São precisas ao menos 6 semanas de histórico'
    );
    expect(within(forecast).getByText('Como a previsão é calculada')).toBeInTheDocument();

    const risks = screen.getByRole('heading', { name: 'Riscos' }).closest('section')!;
    const list = within(risks).getByRole('list', { name: 'Riscos detectados' });
    const [first] = within(list).getAllByRole('listitem');
    expect(first).toHaveTextContent('Itens com prazo vencido');
    expect(first).toHaveTextContent('Severidade alta');
    expect(first).toHaveTextContent('O que fazer:');

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

describe('Gestão · execuções ligadas a itens', () => {
  it('sem fonte vinculada a seção nem aparece', async () => {
    const engine = fakeIntegrationEngine();
    renderPage(engine.client, loja);

    await screen.findByText(/Não há dados de atividades/);

    expect(screen.queryByRole('heading', { name: 'Execuções ligadas a itens' })).not.toBeInTheDocument();
    expect(engine.calls.some((call) => call.method === 'run.linked')).toBe(false);
  });

  it('com fonte e sem ligação diz como ligar, apontando para Execuções', async () => {
    const engine = fakeIntegrationEngine();
    engine.seed(loja.projectId);
    renderPage(engine.client, loja);

    const section = (await screen.findByRole('heading', { name: 'Execuções ligadas a itens' })).closest('section')!;

    expect(
      await within(section).findByText(/Nenhuma execução foi ligada a um item deste projeto ainda/)
    ).toBeInTheDocument();
    expect(within(section).getByRole('link', { name: 'Execuções' })).toHaveAttribute('href', '/execucoes');
  });

  it('mostra o item com título e situação e as execuções ligadas a ele', async () => {
    const engine = fakeIntegrationEngine();
    engine.seed(loja.projectId);
    engine.seedLinked(linkedFixture.items);
    renderPage(engine.client, loja);

    const section = (await screen.findByRole('heading', { name: 'Execuções ligadas a itens' })).closest('section')!;

    const items = await within(section).findByRole('list', { name: 'Itens com execuções ligadas' });
    expect(items).toHaveTextContent('#41');
    expect(items).toHaveTextContent('Pagamento por Pix');
    expect(items).toHaveTextContent('GitHub');
    const runs = within(items).getByRole('list', { name: 'Execuções ligadas ao item 41' });
    expect(runs.children).toHaveLength(2);
    expect(runs.children[0]).toHaveTextContent('Workflow · Falhou');
    expect(runs.children[1]).toHaveTextContent('Configuração aplicada · Concluído');
  });

  it('um item que saiu do recorte aparece sem título', async () => {
    const engine = fakeIntegrationEngine();
    engine.seed(loja.projectId);
    engine.seedLinked([
      { ...linkedFixture.items[0]!, available: false, title: null, status: null, statusCategory: null }
    ]);
    renderPage(engine.client, loja);

    const section = (await screen.findByRole('heading', { name: 'Execuções ligadas a itens' })).closest('section')!;

    expect(await within(section).findByText('fora do recorte atual da integração')).toBeInTheDocument();
    expect(section).not.toHaveTextContent('Pagamento por Pix');
  });
});
