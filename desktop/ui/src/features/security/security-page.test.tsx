import planFixture from '@contracts/fixtures/security/plan.json';
import scanFixture from '@contracts/fixtures/security/scan.json';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router';
import { describe, expect, it } from 'vitest';

import type { EngineClient } from '@/shared/api';
import { AppProviders } from '@/shared/components';
import { expectNoA11yViolations } from '@/test/a11y';

import { SecurityPage } from './security-page';

const loja = { projectId: 'p-000000000001', displayName: 'Loja', displayPath: '~\\Loja' };

const fixResult = {
  runId: 'run-sec-1',
  kind: 'security',
  outcome: 'completed',
  changes: [{ path: '.gitignore', op: 'update', status: 'applied' }],
  fixed: 4,
  remaining: 3,
  error: null
};

const snapshot = (runId: string, result: object) => ({
  runId,
  projectId: loja.projectId,
  kind: 'security',
  state: 'completed',
  lastSeq: 3,
  events: [],
  result
});

/** O motor responde já com a execução terminada: a tela não depende da ordem dos eventos. */
function fakeEngine(report: object = scanFixture) {
  const calls: { method: string; params: Record<string, unknown> }[] = [];
  const client: EngineClient = {
    async request(method, params, schema) {
      calls.push({ method, params: params as Record<string, unknown> });
      const runId = (params as { runId?: string }).runId;
      const reply: Record<string, unknown> = {
        'security.scan': report,
        'security.plan': planFixture,
        'security.fix': { runId: 'run-sec-1' },
        'harness.revert': { runId: 'run-rev-1' },
        'run.snapshot': snapshot(runId ?? '', runId === 'run-sec-1' ? fixResult : { outcome: 'completed' })
      };
      return schema.parse(reply[method]);
    },
    async onEvent() {
      return () => undefined;
    }
  };
  return { client, calls };
}

function renderPage(client: EngineClient, project: typeof loja | null = loja) {
  return render(
    <AppProviders>
      <MemoryRouter>
        <SecurityPage client={client} project={project} />
      </MemoryRouter>
    </AppProviders>
  );
}

describe('Segurança', () => {
  it('separa o que o Spock corrige do que precisa da pessoa, sem mostrar valores', async () => {
    const { container } = renderPage(fakeEngine().client);

    const fixable = await screen.findByRole('region', { name: 'O Spock corrige com a sua aprovação' });
    const manual = screen.getByRole('region', { name: 'Precisa de você' });
    expect(screen.getByRole('status')).toHaveTextContent('7 achados, 4 graves, em 6 arquivos lidos.');
    expect(within(fixable).getByText('.env')).toBeInTheDocument();
    expect(
      within(fixable).getByText('O Claude Code está configurado para pular todas as confirmações')
    ).toBeInTheDocument();
    expect(within(manual).getByText('O Codex roda sem sandbox')).toBeInTheDocument();
    expect(within(manual).getByText('src/config.ts, linha 2')).toBeInTheDocument();
    expect(container).not.toHaveTextContent(/ghp_/);
    await expectNoA11yViolations(container);
  });

  it('mostra a diferença, aplica só depois de confirmar e varre de novo', async () => {
    const engine = fakeEngine();
    renderPage(engine.client);

    await userEvent.click(await screen.findByRole('button', { name: 'Revisar a correção' }));
    const review = await screen.findByRole('region', { name: 'Revisar a correção' });
    expect(within(review).getByLabelText('Diferenças em .gitignore')).toHaveTextContent('+/.env');
    expect(review).toHaveTextContent('Corrige 4 achados mudando 2 arquivos');
    expect(engine.calls.some((call) => call.method === 'security.fix')).toBe(false);

    await userEvent.click(within(review).getByRole('button', { name: 'Aplicar correção' }));
    await userEvent.click(screen.getByRole('button', { name: 'Aplicar' }));

    expect(await screen.findByText('Correção aplicada')).toBeInTheDocument();
    expect(screen.getByText(/4 achados resolvidos; 3 continuam/)).toBeInTheDocument();
    expect(engine.calls.find((call) => call.method === 'security.fix')?.params).toEqual({
      projectId: loja.projectId,
      planId: planFixture.planId
    });
    expect(engine.calls.filter((call) => call.method === 'security.scan')).toHaveLength(2);
  });

  it('a correção pode ser desfeita, também com confirmação', async () => {
    const engine = fakeEngine();
    renderPage(engine.client);

    await userEvent.click(await screen.findByRole('button', { name: 'Revisar a correção' }));
    await userEvent.click(await screen.findByRole('button', { name: 'Aplicar correção' }));
    await userEvent.click(screen.getByRole('button', { name: 'Aplicar' }));
    await userEvent.click(await screen.findByRole('button', { name: 'Desfazer correção' }));
    await userEvent.click(screen.getByRole('button', { name: 'Desfazer' }));

    expect(await screen.findByText('Correção desfeita')).toBeInTheDocument();
    expect(engine.calls.find((call) => call.method === 'harness.revert')?.params).toEqual({
      projectId: loja.projectId,
      runId: 'run-sec-1'
    });
  });

  it('sem achados, diz que não encontrou e o que isso não garante', async () => {
    renderPage(fakeEngine({ ...scanFixture, findings: [] }).client);

    expect(await screen.findByText('Nenhum problema encontrado')).toBeInTheDocument();
    expect(screen.getByRole('status')).toHaveTextContent('Nada encontrado em 6 arquivos lidos.');
    expect(screen.queryByRole('button', { name: 'Revisar a correção' })).not.toBeInTheDocument();
  });

  it('sem projeto aberto, pede para escolher um', async () => {
    const engine = fakeEngine();
    renderPage(engine.client, null);

    expect(await screen.findByRole('heading', { name: 'Segurança' })).toBeInTheDocument();
    expect(engine.calls).toHaveLength(0);
  });
});
