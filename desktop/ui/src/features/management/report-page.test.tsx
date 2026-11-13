import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router';
import { afterEach, describe, expect, it, vi } from 'vitest';

import type { SelectedProject } from '@/features/project-selection';
import { EngineError, type EngineClient } from '@/shared/api';
import { AppProviders } from '@/shared/components';
import { fakeIntegrationEngine } from '@/test/fake-integration-engine';

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

const reportCalls = (calls: { method: string; params: Record<string, unknown> }[]) =>
  calls.filter((call) => call.method === 'management.report');

afterEach(() => vi.restoreAllMocks());

describe('Relatório', () => {
  it('sem projeto aberto não pede nada ao motor', () => {
    const engine = fakeIntegrationEngine();
    renderPage(engine.client, null);

    expect(screen.getByText(/Nenhum projeto selecionado/)).toBeInTheDocument();
    expect(engine.calls).toEqual([]);
  });

  it('pede quatro semanas e mostra as seções do relatório', async () => {
    const engine = fakeIntegrationEngine();
    renderPage(engine.client);

    expect(await screen.findByRole('heading', { name: 'Decisões do período' })).toBeInTheDocument();

    expect(reportCalls(engine.calls)[0]?.params).toEqual({ projectId: loja.projectId, weeks: 4 });
    for (const title of [
      'Fluxo nas últimas 4 semanas',
      'Previsão de entrega',
      'Marcos',
      'Riscos',
      'Mudanças de risco no período',
      'Execuções do período'
    ])
      expect(screen.getByRole('heading', { name: title })).toBeInTheDocument();
    expect(screen.getByRole('list', { name: 'Decisões do período' })).toHaveTextContent('Adiar a release');
    expect(screen.getByRole('list', { name: 'Execuções do período' })).toHaveTextContent('Workflow, falhou: 1');
  });

  it('trocar o período pede o relatório de novo com as semanas escolhidas', async () => {
    const engine = fakeIntegrationEngine();
    renderPage(engine.client);
    await screen.findByRole('heading', { name: 'Decisões do período' });

    await userEvent.selectOptions(screen.getByLabelText('Largura do período'), '12');

    await waitFor(() =>
      expect(reportCalls(engine.calls).at(-1)?.params).toEqual({ projectId: loja.projectId, weeks: 12 })
    );
  });

  it('copia o relatório como texto e diz que copiou', async () => {
    const writeText = vi.fn(async () => undefined);
    Object.defineProperty(navigator, 'clipboard', { value: { writeText }, configurable: true });
    const engine = fakeIntegrationEngine();
    renderPage(engine.client);

    await userEvent.click(await screen.findByRole('button', { name: 'Copiar como texto' }));

    await waitFor(() => expect(writeText).toHaveBeenCalledTimes(1));
    const text = (writeText.mock.calls as unknown as string[][])[0]![0]!;
    expect(text).toContain('# Relatório do período:');
    expect(text).toContain('## Decisões do período');
    expect(await screen.findByText('Relatório copiado.')).toBeInTheDocument();
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

    await userEvent.click(await screen.findByRole('button', { name: 'Copiar como texto' }));

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
