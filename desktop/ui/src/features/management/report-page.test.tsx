import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import type { SelectedProject } from '@/features/project-selection';
import { EngineError, type EngineClient } from '@/shared/api';
import { AppProviders } from '@/shared/components';
import { projectReportSchema } from '@/shared/schema';
import { expectNoA11yViolations } from '@/test/a11y';
import { fakeIntegrationEngine } from '@/test/fake-integration-engine';
import { chooseOption } from '@/test/choose-option';
import reportFixture from '@contracts/fixtures/management/report.json';

import { ReportPage } from './report-page';

const loja: SelectedProject = { projectId: 'p-aaaaaaaaaaaa', displayName: 'Loja', displayPath: '~\\Loja' };

const renderPage = (client: EngineClient, project: SelectedProject | null = loja) =>
  render(
    <AppProviders>
      <MemoryRouter>
        <ReportPage client={client} project={project} />
      </MemoryRouter>
    </AppProviders>
  );

const parsedReport = projectReportSchema.parse(reportFixture);

const withReport = (report: typeof parsedReport): EngineClient => {
  const engine = fakeIntegrationEngine();
  return {
    ...engine.client,
    request: async (method, params, schema) =>
      method === 'management.report' ? schema.parse(report) : engine.client.request(method, params, schema)
  };
};

/** Os títulos de seção (h2) na ordem em que aparecem, sem o título da página. */
const sectionTitles = () => screen.getAllByRole('heading', { level: 2 }).map((heading) => heading.textContent);

const SECTION_ORDER = [
  'Previsão de entrega',
  'Fluxo nas últimas 4 semanas',
  'Marcos',
  'Riscos',
  'Mudanças de risco no período',
  'Decisões do período',
  'Execuções do período'
];

const reportCalls = (calls: { method: string; params: Record<string, unknown> }[]) =>
  calls.filter((call) => call.method === 'management.report');

beforeEach(() => localStorage.clear());
afterEach(() => vi.restoreAllMocks());

describe('Relatório', () => {
  it('sem projeto aberto não pede nada ao motor', () => {
    const engine = fakeIntegrationEngine();
    renderPage(engine.client, null);

    expect(screen.getByText(/Nenhum projeto selecionado/)).toBeInTheDocument();
    expect(engine.calls).toEqual([]);
  });

  it('pede o período comum, de oito semanas, e mostra as seções do relatório', async () => {
    const engine = fakeIntegrationEngine();
    renderPage(engine.client);

    expect(await screen.findByRole('heading', { name: 'Decisões do período' })).toBeInTheDocument();

    expect(reportCalls(engine.calls)[0]?.params).toEqual({ projectId: loja.projectId, weeks: 8 });
    expect(screen.getByRole('list', { name: 'Decisões do período' })).toHaveTextContent('Adiar a release');
    // Tipo e estado são rótulos: o estado no masculino não concorda com o tipo no feminino.
    expect(screen.getByRole('list', { name: 'Execuções do período' })).toHaveTextContent(
      'Pedido de engenharia · Falhou · 1 execução'
    );
  });

  it('segue a ordem das abas do Acompanhamento, com os mesmos nomes', async () => {
    const engine = fakeIntegrationEngine();
    renderPage(engine.client);
    await screen.findByRole('heading', { name: 'Decisões do período' });

    expect(sectionTitles()).toEqual(SECTION_ORDER);
  });

  it('sem dado, a seção continua no lugar e diz o que falta, como o Acompanhamento', async () => {
    renderPage(
      withReport({
        ...parsedReport,
        forecast: { ...parsedReport.forecast, kinds: [] },
        metrics: { ...parsedReport.metrics, kinds: [] },
        risks: { ...parsedReport.risks, hasData: false, risks: [] }
      })
    );
    await screen.findByRole('heading', { name: 'Decisões do período' });

    expect(sectionTitles()).toEqual(SECTION_ORDER);
    expect(screen.getByRole('heading', { name: 'Previsão de entrega' }).closest('section')).toHaveTextContent(
      'Sem dados para calcular a previsão ainda.'
    );
    expect(screen.getByRole('heading', { name: 'Fluxo nas últimas 4 semanas' }).closest('section')).toHaveTextContent(
      'Sem dados para calcular o fluxo ainda.'
    );
    expect(screen.getByRole('heading', { name: 'Riscos' }).closest('section')).toHaveTextContent(
      'Sem dados para detectar riscos ainda.'
    );
    expect(screen.getAllByRole('link', { name: 'Conexões › Serviços' })).toHaveLength(3);
  });

  it('as seções do período dizem que estão vazias em vez de sumir', async () => {
    renderPage(withReport({ ...parsedReport, riskChanges: [], decisions: [], runs: [] }));

    expect(await screen.findByText('Nenhuma decisão registrada no período.')).toBeInTheDocument();
    expect(screen.getByText('Nenhuma mudança de risco registrada no período.')).toBeInTheDocument();
    expect(screen.getByText('Nenhuma execução começou no período.')).toBeInTheDocument();
  });

  it('a ação principal é copiar, no cabeçalho, e não há seção "Para levar"', async () => {
    const engine = fakeIntegrationEngine();
    const { container } = renderPage(engine.client);

    const copy = await screen.findByRole('button', { name: 'Copiar como Markdown' });
    expect(copy.closest('header')).toContainElement(screen.getByRole('heading', { level: 1, name: 'Relatório' }));
    expect(screen.queryByRole('heading', { name: 'Para levar' })).not.toBeInTheDocument();
    await expectNoA11yViolations(container);
  });

  it('trocar o período pede o relatório de novo com as semanas escolhidas', async () => {
    const engine = fakeIntegrationEngine();
    renderPage(engine.client);
    await screen.findByRole('heading', { name: 'Decisões do período' });

    await chooseOption('Período', 'Últimas 12 semanas');

    await waitFor(() =>
      expect(reportCalls(engine.calls).at(-1)?.params).toEqual({ projectId: loja.projectId, weeks: 12 })
    );
  });

  it('abre no período escolhido antes em outra tela', async () => {
    localStorage.setItem('spock.management.periodWeeks', '12');
    const engine = fakeIntegrationEngine();
    renderPage(engine.client);

    await screen.findByRole('heading', { name: 'Decisões do período' });
    expect(reportCalls(engine.calls)[0]?.params).toEqual({ projectId: loja.projectId, weeks: 12 });
  });

  it('copia o relatório como texto e diz que copiou', async () => {
    const writeText = vi.fn(async () => undefined);
    Object.defineProperty(navigator, 'clipboard', { value: { writeText }, configurable: true });
    const engine = fakeIntegrationEngine();
    renderPage(engine.client);

    await userEvent.click(await screen.findByRole('button', { name: 'Copiar como Markdown' }));

    await waitFor(() => expect(writeText).toHaveBeenCalledTimes(1));
    const text = (writeText.mock.calls as unknown as string[][])[0]![0]!;
    expect(text).toContain('# Relatório do período:');
    expect(text).toContain('## Decisões do período');
    expect(await screen.findByText('Relatório copiado.')).toBeInTheDocument();
    // A confirmação tem lugar próprio: o período continua à vista, sem a hora.
    expect(screen.getByText(/Período: de 15\/10\/2026 a 12\/11\/2026\./)).toBeInTheDocument();
  });

  it('se a cópia falha diz como seguir', async () => {
    Object.defineProperty(navigator, 'clipboard', {
      value: {
        writeText: vi.fn(async () => {
          throw new Error('negado');
        })
      },
      configurable: true
    });
    const engine = fakeIntegrationEngine();
    renderPage(engine.client);

    await userEvent.click(await screen.findByRole('button', { name: 'Copiar como Markdown' }));

    expect(await screen.findByText(/Não foi possível copiar/)).toBeInTheDocument();
  });

  it('mostra o motivo quando o motor recusa e não deixa a tela vazia sem explicação', async () => {
    const engine = fakeIntegrationEngine();
    const failing: EngineClient = {
      ...engine.client,
      request: async (method, params, schema) => {
        if (method === 'management.report')
          throw new EngineError('PROJECT_NOT_AUTHORIZED', 'O projeto não foi autorizado.');
        return engine.client.request(method, params, schema);
      }
    };
    renderPage(failing);

    expect(await screen.findByRole('alert')).toHaveTextContent('O projeto não foi autorizado.');
  });

  it('a base do motor, dizendo que nada foi escrito por modelo, fica à vista', async () => {
    const engine = fakeIntegrationEngine();
    renderPage(engine.client);

    expect(await screen.findByText(/nenhum texto foi escrito por modelo de linguagem/)).toBeInTheDocument();
  });
});
