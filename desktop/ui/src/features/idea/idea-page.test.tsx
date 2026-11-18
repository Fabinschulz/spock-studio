import draftFixture from '@contracts/fixtures/idea/draft.json';
import planFixture from '@contracts/fixtures/idea/plan.json';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router';
import { describe, expect, it } from 'vitest';

import { EngineError, type EngineClient } from '@/shared/api';
import { AppProviders } from '@/shared/components';
import { expectNoA11yViolations } from '@/test/a11y';

import { IdeaPage } from './idea-page';

const loja = { projectId: 'p-000000000001', displayName: 'Loja', displayPath: '~\\Loja' };
const IDEA = 'Um app para lojistas acompanharem pedidos e avisarem o cliente da entrega.';

const passed = [{ id: 'shape', label: 'Tem o que precisa', passed: true, detail: null }];
const drafts = {
  specification: draftFixture,
  architecture: {
    stage: 'architecture',
    artifact: {
      style: 'Monólito modular',
      summary: 'Uma API e um painel.',
      components: [{ id: 'CMP-01', name: 'Pedidos', responsibility: 'Guarda os pedidos.', requirements: ['REQ-01'] }]
    },
    checks: passed,
    valid: true,
    attempts: 1,
    inputTokens: 900,
    outputTokens: 300
  },
  backlog: {
    stage: 'backlog',
    artifact: {
      epics: [
        {
          id: 'EPIC-01',
          title: 'Ciclo do pedido',
          stories: [
            {
              id: 'US-01',
              title: 'Como lojista, quero registrar pedidos',
              requirements: ['REQ-01'],
              acceptance: ['Dado um pedido novo, quando salvo, então aparece'],
              estimate: 'P'
            }
          ]
        }
      ]
    },
    checks: passed,
    valid: true,
    attempts: 1,
    inputTokens: 1000,
    outputTokens: 500
  }
} as const;

function fakeEngine(options: { draft?: (stage: string) => object | Error } = {}) {
  const calls: { method: string; params: Record<string, unknown> }[] = [];
  const client: EngineClient = {
    async request(method, params, schema) {
      const body = params as Record<string, unknown>;
      calls.push({ method, params: body });
      if (method === 'idea.draft') {
        const stage = body.stage as keyof typeof drafts;
        const reply = options.draft?.(stage) ?? drafts[stage];
        if (reply instanceof Error) throw reply;
        return schema.parse(reply);
      }
      const runId = body.runId as string | undefined;
      const reply: Record<string, unknown> = {
        'idea.plan': planFixture,
        'idea.save': { runId: 'run-idea-1' },
        'harness.revert': { runId: 'run-rev-1' },
        'run.snapshot': {
          runId,
          projectId: loja.projectId,
          kind: 'idea',
          state: 'completed',
          lastSeq: 2,
          events: [],
          result:
            runId === 'run-idea-1'
              ? { runId, kind: 'idea', outcome: 'completed', files: [], error: null }
              : { outcome: 'completed' }
        }
      };
      return schema.parse(reply[method]);
    },
    async onEvent() {
      return () => undefined;
    }
  };
  return { client, calls };
}

function renderPage(client: EngineClient) {
  return render(
    <AppProviders>
      <MemoryRouter>
        <IdeaPage client={client} project={loja} />
      </MemoryRouter>
    </AppProviders>
  );
}

// Colar, e não digitar letra a letra: a ideia é longa e o teste só precisa do texto no campo.
async function describeIdea() {
  await userEvent.click(screen.getByLabelText(/Sua ideia/));
  await userEvent.paste(IDEA);
  await userEvent.click(screen.getByRole('button', { name: 'Gerar especificação' }));
}

describe('Da ideia ao projeto', () => {
  it('leva a ideia por especificação, arquitetura e backlog, com aprovação em cada etapa, e grava os documentos', async () => {
    const engine = fakeEngine();
    const { container } = renderPage(engine.client);

    await describeIdea();
    const spec = await screen.findByRole('region', { name: '1. Especificação' });
    expect(within(spec).getByText('Passou nas verificações')).toBeInTheDocument();
    expect(spec).toHaveTextContent(
      'REQ-02 (must): Quando o pedido sair para entrega, o sistema deve avisar o cliente.'
    );
    expect(within(spec).getByRole('list', { name: 'Verificações do motor' })).toHaveTextContent('Passou:');
    await expectNoA11yViolations(container);

    await userEvent.click(within(spec).getByRole('button', { name: 'Aprovar e seguir' }));
    expect(screen.getByLabelText(/Sua ideia/)).toBeDisabled();
    await userEvent.click(screen.getByRole('button', { name: 'Gerar arquitetura' }));
    const architecture = await screen.findByRole('region', { name: '2. Arquitetura' });
    expect(architecture).toHaveTextContent('CMP-01 Pedidos');
    expect(engine.calls.filter((call) => call.method === 'idea.draft')[1]?.params).toMatchObject({
      stage: 'architecture',
      idea: IDEA,
      specification: draftFixture.artifact
    });

    await userEvent.click(within(architecture).getByRole('button', { name: 'Aprovar e seguir' }));
    await userEvent.click(screen.getByRole('button', { name: 'Gerar backlog' }));
    const backlog = await screen.findByRole('region', { name: '3. Backlog' });
    await userEvent.click(within(backlog).getByRole('button', { name: 'Aprovar e seguir' }));

    await userEvent.click(screen.getByRole('button', { name: 'Revisar os documentos' }));
    const review = await screen.findByRole('region', { name: 'Revisar os documentos' });
    expect(within(review).getByLabelText('Diferenças em .spock/arquitetura/especificacao.md')).toBeInTheDocument();
    expect(engine.calls.some((call) => call.method === 'idea.save')).toBe(false);

    await userEvent.click(within(review).getByRole('button', { name: 'Gravar documentos' }));
    await userEvent.click(screen.getByRole('button', { name: 'Gravar' }));

    expect(await screen.findByText('Documentos gravados')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'configurar o Harness' })).toHaveAttribute('href', '/configurar');
    expect(engine.calls.find((call) => call.method === 'idea.save')?.params).toMatchObject({
      projectId: loja.projectId,
      planId: planFixture.planId,
      backlog: drafts.backlog.artifact
    });

    await userEvent.click(screen.getByRole('button', { name: 'Desfazer gravação' }));
    await userEvent.click(screen.getByRole('button', { name: 'Desfazer' }));
    expect(await screen.findByText('A gravação anterior foi desfeita.')).toBeInTheDocument();
    expect(engine.calls.find((call) => call.method === 'harness.revert')?.params).toEqual({
      projectId: loja.projectId,
      runId: 'run-idea-1'
    });
    // O fluxo inteiro, das três etapas ao desfazer, passa de dezenas de cliques.
  }, 20_000);

  it('etapa que não passou nas verificações não pode ser aprovada e pode ser gerada de novo', async () => {
    const failing = {
      ...draftFixture,
      valid: false,
      attempts: 2,
      checks: [{ id: 'coverage', label: 'Todo requisito tem ao menos uma história', passed: false, detail: 'REQ-02' }]
    };
    const engine = fakeEngine({ draft: () => failing });
    renderPage(engine.client);

    await describeIdea();
    const spec = await screen.findByRole('region', { name: '1. Especificação' });
    expect(within(spec).getByText('Não passou')).toBeInTheDocument();
    expect(spec).toHaveTextContent('Falhou: Todo requisito tem ao menos uma história (REQ-02)');
    expect(within(spec).queryByRole('button', { name: 'Aprovar e seguir' })).not.toBeInTheDocument();

    await userEvent.click(within(spec).getByRole('button', { name: 'Gerar de novo' }));
    expect(engine.calls.filter((call) => call.method === 'idea.draft')).toHaveLength(2);
  });

  it('sem modelo configurado, mostra o motivo e onde configurar', async () => {
    renderPage(
      fakeEngine({ draft: () => new EngineError('MODEL_NOT_CONFIGURED', 'Nenhum modelo está configurado.') }).client
    );

    await describeIdea();

    expect(await screen.findByText('Nenhum modelo está configurado.')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Minha IA' })).toHaveAttribute('href', '/configuracoes/ia');
  });

  it('só gera com uma ideia descrita', async () => {
    renderPage(fakeEngine().client);

    await userEvent.type(screen.getByLabelText(/Sua ideia/), 'curta');

    expect(screen.getByRole('button', { name: 'Gerar especificação' })).toBeDisabled();
  });
});
