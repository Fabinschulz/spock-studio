import listFixture from '@contracts/fixtures/golden/quality-list.json';
import reviewFixture from '@contracts/fixtures/golden/quality-review.json';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router';
import { describe, expect, it } from 'vitest';

import { EngineError, type EngineClient } from '@/shared/api';
import { AppProviders } from '@/shared/components';
import { expectNoA11yViolations } from '@/test/a11y';

import { QualityPage } from './quality-page';

const loja = { projectId: 'p-000000000001', displayName: 'Loja', displayPath: '~\\Loja' };

type Options = {
  fail?: { code: string; message: string };
  replies?: Record<string, unknown>;
  modelAvailable?: boolean;
};

const gate = {
  id: 'test:.:npm',
  kind: 'test',
  tool: 'npm',
  command: 'npm run test',
  directory: '.',
  origin: 'script "test" do package.json'
};

function fakeEngine(options: Options = {}) {
  const calls: { method: string; params: Record<string, unknown> }[] = [];
  const client: EngineClient = {
    async request(method, params, schema) {
      calls.push({ method, params: params as Record<string, unknown> });
      if (method === 'quality.review.start' && options.fail)
        throw new EngineError(options.fail.code, options.fail.message);
      const reply: Record<string, unknown> = {
        'quality.review.start': reviewFixture,
        'quality.review.detail': reviewFixture,
        'quality.review.list': listFixture,
        'quality.gates.list': { gates: [gate], modelAvailable: options.modelAvailable ?? true },
        ...options.replies
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
        <QualityPage client={client} project={project} />
      </MemoryRouter>
    </AppProviders>
  );
}

describe('Revisão de código', () => {
  it('pede um projeto antes de qualquer coisa', () => {
    renderPage(fakeEngine().client, null);

    expect(screen.getByRole('heading', { name: 'Revisão de código' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Revisar alterações locais' })).not.toBeInTheDocument();
  });

  it('revisa as alterações locais e separa confirmados, a conferir e recomendações, sem mostrar segredo', async () => {
    const engine = fakeEngine();
    const { container } = renderPage(engine.client);

    await userEvent.click(await screen.findByRole('button', { name: 'Revisar alterações locais' }));

    const confirmed = await screen.findByRole('region', { name: 'Problemas confirmados' });
    const toCheck = screen.getByRole('region', { name: 'A conferir' });
    expect(within(confirmed).getByText('Credencial de um provedor no código alterado')).toBeInTheDocument();
    expect(within(toCheck).getByText('Erro engolido por um tratamento vazio')).toBeInTheDocument();
    expect(screen.getAllByText('Reprovada').length).toBeGreaterThanOrEqual(1);
    expect(engine.calls.find((call) => call.method === 'quality.review.start')?.params).toEqual({
      projectId: loja.projectId,
      mode: 'working_tree'
    });
    expect(container).not.toHaveTextContent(/ghp_/);
    await expectNoA11yViolations(container);
  });

  it('diz quem avaliou e o que não foi verificado: nenhum juiz de IA rodou', async () => {
    renderPage(fakeEngine().client);

    await userEvent.click(await screen.findByRole('button', { name: 'Revisar alterações locais' }));

    const judges = await screen.findByRole('region', { name: 'Quem avaliou' });
    expect(within(judges).getByText('Revisão por modelo de IA')).toBeInTheDocument();
    expect(within(judges).getAllByText('Não executada').length).toBeGreaterThanOrEqual(3);
    const limits = screen.getByRole('region', { name: 'O que não foi verificado' });
    expect(limits).toHaveTextContent('build, testes e lint não rodaram');
  });

  it('o aviso não repete o veredito no texto', async () => {
    renderPage(fakeEngine().client);

    await userEvent.click(await screen.findByRole('button', { name: 'Revisar alterações locais' }));

    const notice = await screen.findByRole('alert');
    expect(notice).toHaveTextContent(/^Reprovada\s*1 problema confirmado e grave/);
    expect(notice).not.toHaveTextContent('Reprovada: ');
  });

  it('um erro tira da tela o relatório anterior', async () => {
    const options: { fail?: { code: string; message: string } } = {};
    renderPage(fakeEngine(options).client);
    await userEvent.click(await screen.findByRole('button', { name: 'Revisar alterações locais' }));
    expect(await screen.findByRole('region', { name: 'Problemas confirmados' })).toBeInTheDocument();

    options.fail = { code: 'REVIEW_REF_NOT_FOUND', message: 'A referência base não existe neste repositório.' };
    await userEvent.type(screen.getByLabelText('Comparar com a base'), 'nao-existe');
    await userEvent.click(screen.getByRole('button', { name: 'Revisar a branch' }));

    expect(await screen.findByText('A referência base não existe neste repositório.')).toBeInTheDocument();
    expect(screen.queryByRole('region', { name: 'Problemas confirmados' })).not.toBeInTheDocument();
  });

  it('a branch só é revisada com a base escrita', async () => {
    const engine = fakeEngine();
    renderPage(engine.client);
    const submit = await screen.findByRole('button', { name: 'Revisar a branch' });
    expect(submit).toBeDisabled();

    await userEvent.type(screen.getByLabelText('Comparar com a base'), 'main');
    await userEvent.click(submit);

    expect(await screen.findByRole('region', { name: 'Problemas confirmados' })).toBeInTheDocument();
    expect(engine.calls.find((call) => call.method === 'quality.review.start')?.params).toEqual({
      projectId: loja.projectId,
      mode: 'branch',
      base: 'main'
    });
  });

  it('mostra o motivo quando a pasta não é um repositório', async () => {
    const engine = fakeEngine({
      fail: { code: 'REVIEW_NOT_A_REPOSITORY', message: 'A pasta do projeto não é um repositório git.' }
    });
    renderPage(engine.client);

    await userEvent.click(await screen.findByRole('button', { name: 'Revisar alterações locais' }));

    expect(await screen.findByText('Não foi possível concluir')).toBeInTheDocument();
    expect(screen.getByText(/não é um repositório git/)).toBeInTheDocument();
  });

  it('o modelo e as verificações do projeto só vão com a escolha e a autorização da pessoa', async () => {
    const engine = fakeEngine();
    renderPage(engine.client);
    const start = await screen.findByRole('button', { name: 'Revisar alterações locais' });

    await userEvent.click(await screen.findByRole('checkbox', { name: /Incluir a revisão por modelo de IA/ }));
    await userEvent.type(screen.getByLabelText('Critérios de aceite (um por linha)'), 'Aplicar o desconto');
    await userEvent.click(screen.getByRole('checkbox', { name: /npm run test/ }));

    // Escolheu um comando do projeto e não autorizou: não dá para começar.
    expect(start).toBeDisabled();
    await userEvent.click(screen.getByRole('checkbox', { name: /Autorizo rodar os comandos marcados/ }));
    expect(start).toBeEnabled();
    await userEvent.click(start);

    expect(await screen.findByRole('region', { name: 'Problemas confirmados' })).toBeInTheDocument();
    expect(engine.calls.find((call) => call.method === 'quality.review.start')?.params).toEqual({
      projectId: loja.projectId,
      mode: 'working_tree',
      useModel: true,
      requirements: 'Aplicar o desconto',
      gates: ['test:.:npm'],
      gatesApproved: true
    });
  });

  it('sem provedor de IA a opção do modelo fica desligada e diz o que fazer', async () => {
    renderPage(fakeEngine({ modelAvailable: false }).client);

    const model = await screen.findByRole('checkbox', { name: /Incluir a revisão por modelo de IA/ });

    expect(model).toBeDisabled();
    expect(screen.getByText(/Configure um provedor de IA em Configurações/)).toBeInTheDocument();
  });

  it('descartar um achado exige a justificativa e a decisão aparece ao lado do achado, que continua lá', async () => {
    const decided = {
      ...reviewFixture,
      decisions: [
        {
          findingId: 'f-000000000001',
          decision: 'dismissed_false_positive',
          justification: 'chave de exemplo do guia, inventada',
          decidedAt: '2026-11-28T12:00:00Z',
          reviewId: reviewFixture.review.id
        }
      ]
    };
    const engine = fakeEngine({ replies: { 'quality.finding.decide': decided } });
    renderPage(engine.client);
    await userEvent.click(await screen.findByRole('button', { name: 'Revisar alterações locais' }));

    await userEvent.click(
      await screen.findByRole('button', { name: 'Descartar o achado Credencial de um provedor no código alterado' })
    );
    const register = screen.getByRole('button', { name: 'Registrar' });
    expect(register).toBeDisabled();
    await userEvent.type(screen.getByLabelText('Justificativa'), 'chave de exemplo do guia, inventada');
    await userEvent.click(register);

    expect(await screen.findByText('Descartado como falso positivo')).toBeInTheDocument();
    expect(screen.getByText(/chave de exemplo do guia, inventada/)).toBeInTheDocument();
    expect(screen.getByText('Credencial de um provedor no código alterado')).toBeInTheDocument();
    expect(engine.calls.find((call) => call.method === 'quality.finding.decide')?.params).toEqual({
      projectId: loja.projectId,
      reviewId: reviewFixture.review.id,
      findingId: 'f-000000000001',
      decision: 'dismissed_false_positive',
      justification: 'chave de exemplo do guia, inventada'
    });
  });

  it('um achado crítico não oferece aceitar o risco, e um médio oferece', async () => {
    renderPage(fakeEngine().client);
    await userEvent.click(await screen.findByRole('button', { name: 'Revisar alterações locais' }));

    await screen.findByRole('region', { name: 'Problemas confirmados' });

    expect(
      screen.queryByRole('button', { name: 'Aceitar o risco do achado Credencial de um provedor no código alterado' })
    ).not.toBeInTheDocument();
    expect(
      screen.getByRole('button', { name: 'Aceitar o risco do achado Erro engolido por um tratamento vazio' })
    ).toBeInTheDocument();
  });

  it('aprovar a entrega só abre quando nada bloqueia, e a assinatura é registrada', async () => {
    const ready = { ...reviewFixture, status: { ...reviewFixture.status, openBlocking: 0, readyForDelivery: true } };
    const signed = {
      ...ready,
      status: {
        ...ready.status,
        delivery: {
          reviewId: reviewFixture.review.id,
          decision: 'approved',
          justification: 'conferi o relatório inteiro',
          decidedAt: '2026-11-28T12:00:00Z'
        }
      }
    };
    const blocked = fakeEngine();
    const { unmount } = renderPage(blocked.client);
    await userEvent.click(await screen.findByRole('button', { name: 'Revisar alterações locais' }));
    await userEvent.type(
      await screen.findByLabelText('Justificativa da decisão sobre a entrega'),
      'conferi o relatório inteiro'
    );
    expect(screen.getByRole('button', { name: 'Aprovar a entrega' })).toBeDisabled();
    unmount();

    const engine = fakeEngine({ replies: { 'quality.review.start': ready, 'quality.delivery.decide': signed } });
    renderPage(engine.client);
    await userEvent.click(await screen.findByRole('button', { name: 'Revisar alterações locais' }));
    await userEvent.type(
      await screen.findByLabelText('Justificativa da decisão sobre a entrega'),
      'conferi o relatório inteiro'
    );
    await userEvent.click(screen.getByRole('button', { name: 'Aprovar a entrega' }));

    expect(await screen.findByText('Entrega aprovada por você')).toBeInTheDocument();
    expect(engine.calls.find((call) => call.method === 'quality.delivery.decide')?.params).toEqual({
      projectId: loja.projectId,
      reviewId: reviewFixture.review.id,
      decision: 'approved',
      justification: 'conferi o relatório inteiro'
    });
    // O veredito do motor continua o dele.
    expect(screen.getAllByText('Reprovada').length).toBeGreaterThanOrEqual(1);
  });

  it('monta o pedido de correção com as regras que o agente não pode quebrar e para no limite de tentativas', async () => {
    const brief = (stopped: boolean) => ({
      brief: {
        reviewId: reviewFixture.review.id,
        attempt: stopped ? 4 : 1,
        maxAttempts: 3,
        stopped,
        items: [],
        constraints: ['Não remova testes.'],
        text: stopped
          ? 'O limite de 3 tentativas foi atingido.'
          : '# Correções pedidas\n## Regras que não mudam\n- Não remova testes.'
      }
    });
    const first = fakeEngine({ replies: { 'quality.review.brief': brief(false) } });
    const { unmount } = renderPage(first.client);
    await userEvent.click(await screen.findByRole('button', { name: 'Revisar alterações locais' }));
    await userEvent.click(await screen.findByRole('button', { name: 'Montar o pedido de correção' }));

    expect(await screen.findByLabelText('Pedido de correção')).toHaveTextContent('Não remova testes.');
    expect(screen.getByText('Tentativa 1 de 3')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Copiar o pedido' })).toBeInTheDocument();
    unmount();

    const stopped = fakeEngine({ replies: { 'quality.review.brief': brief(true) } });
    renderPage(stopped.client);
    await userEvent.click(await screen.findByRole('button', { name: 'Revisar alterações locais' }));
    await userEvent.click(await screen.findByRole('button', { name: 'Montar o pedido de correção' }));

    expect(await screen.findByText('Limite de tentativas')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Copiar o pedido' })).not.toBeInTheDocument();
  });

  it('mostra o resultado de cada verificação, o que o modelo viu e os critérios', async () => {
    const extras = {
      ...reviewFixture,
      review: {
        ...reviewFixture.review,
        gates: [
          {
            id: 'test:.:npm',
            kind: 'test',
            tool: 'npm',
            command: 'npm run test',
            directory: '.',
            origin: 'script "test" do package.json',
            state: 'failed',
            exitCode: 1,
            durationMs: 1200,
            summary: 'Terminou com código 1 em 1,2 s.',
            logTail: 'expected 2 to be 3'
          }
        ],
        requirements: [
          {
            index: 1,
            text: 'Aplicar o desconto',
            status: 'unknown',
            path: null,
            line: null,
            excerpt: null,
            note: 'sem evidência'
          }
        ],
        modelRuns: [
          {
            purpose: 'review',
            state: 'used',
            model: 'modelo-x',
            modelVersion: 'v1',
            promptHash: 'abc',
            inputTokens: 100,
            outputTokens: 20,
            accepted: 1,
            discarded: 2,
            failureCode: null
          }
        ]
      }
    };
    renderPage(fakeEngine({ replies: { 'quality.review.start': extras } }).client);

    await userEvent.click(await screen.findByRole('button', { name: 'Revisar alterações locais' }));

    const gates = await screen.findByRole('region', { name: 'Verificações do projeto' });
    expect(within(gates).getByText('Falhou')).toBeInTheDocument();
    expect(within(gates).getByLabelText('Saída de npm run test')).toHaveTextContent('expected 2 to be 3');
    expect(screen.getByRole('region', { name: 'Critérios de aceite' })).toHaveTextContent('Sem evidência');
    expect(screen.getByRole('region', { name: 'Uso do modelo de IA' })).toHaveTextContent('2 descartada(s)');
  });

  it('lista o histórico e abre uma revisão anterior', async () => {
    const engine = fakeEngine();
    renderPage(engine.client);

    await userEvent.click(await screen.findByRole('button', { name: /^Abrir a revisão de / }));

    expect(await screen.findByRole('region', { name: 'Problemas confirmados' })).toBeInTheDocument();
    expect(engine.calls.some((call) => call.method === 'quality.review.detail')).toBe(true);
  });
});
