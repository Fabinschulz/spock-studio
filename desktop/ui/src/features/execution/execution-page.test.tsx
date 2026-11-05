import { act, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';

import type { EngineClient } from '@/shared/api';
import type { RunEvent } from '@/shared/schema';
import { AppProviders } from '@/shared/components';
import { expectNoA11yViolations } from '@/test/a11y';

import { ExecutionPage, workflowSnapshotSchema, type WorkflowSnapshot } from './execution-page';

const paused: WorkflowSnapshot = {
  runId: 'run-1',
  projectId: 'p-aaaaaaaaaaaa',
  kind: 'workflow',
  state: 'awaiting_approval',
  lastSeq: 1,
  events: [{ seq: 1, type: 'agent.completed', data: { agentId: 'architecture' } }],
  result: null
};

const done: WorkflowSnapshot = {
  ...paused,
  state: 'completed_with_limitations',
  result: {
    kind: 'workflow',
    summary: '1 etapa(s). Stacks detectadas: 1. Trechos: 1. Nenhuma alteração no projeto.',
    evidence: [{ filePath: 'docs/nota.md', excerpt: 'trecho visível', chunkId: 'docs/nota.md#1' }],
    limitations: [{ code: 'JUDGE_NOT_RUN', message: 'O juiz por modelo não é o veredito desta fase.' }],
    impact: { schema: 'engineering.impact.v1', areas: ['servico'], risks: [], evidence: ['docs/nota.md#1'] }
  }
};

function clientFor(
  first: WorkflowSnapshot,
  second?: WorkflowSnapshot
): EngineClient & { calls: string[]; resumes: unknown[] } {
  const calls: string[] = [];
  const resumes: unknown[] = [];
  let reads = 0;
  return {
    calls,
    resumes,
    async request(method, params, schema) {
      calls.push(method);
      if (method === 'workflow.resume') resumes.push(params);
      if (method === 'run.snapshot') {
        reads += 1;
        return schema.parse(reads === 1 ? first : (second ?? first));
      }
      if (method === 'workflow.resume') return schema.parse({ runId: first.runId });
      throw new Error(method);
    },
    async onEvent() {
      return () => undefined;
    }
  };
}

describe('Execução', () => {
  it('mostra o agente que de fato concluiu e pede aprovação', async () => {
    const client = clientFor(paused);
    const { container } = render(
      <AppProviders>
        <ExecutionPage runId="run-1" client={client} />
      </AppProviders>
    );

    expect(await screen.findByRole('heading', { name: 'Execução' })).toBeInTheDocument();
    expect(screen.getByText('Aguardando aprovação')).toBeInTheDocument();
    expect(screen.getByText('Arquitetura concluiu')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Aprovar' })).toBeEnabled();
    await expectNoA11yViolations(container);
  });

  it('aprova e então mostra evidência, impacto e limitação reais', async () => {
    const client = clientFor(paused, done);
    render(
      <AppProviders>
        <ExecutionPage runId="run-1" client={client} />
      </AppProviders>
    );
    await userEvent.click(await screen.findByRole('button', { name: 'Aprovar' }));

    expect(await screen.findByText('Concluído com limitações')).toBeInTheDocument();
    expect(screen.getByText('docs/nota.md')).toBeInTheDocument();
    expect(screen.getByText('trecho visível')).toBeInTheDocument();
    expect(screen.getByText('Serviço')).toBeInTheDocument();
    expect(screen.getByText('O juiz por modelo não é o veredito desta fase.')).toBeInTheDocument();
    expect(client.calls).toEqual(['run.snapshot', 'workflow.resume', 'run.snapshot']);
    expect(screen.queryByRole('button', { name: 'Aprovar' })).not.toBeInTheDocument();
  });

  it('acompanha os eventos ao vivo e recupera a lacuna pelo sinceSeq', async () => {
    const running: WorkflowSnapshot = {
      runId: 'run-1',
      projectId: 'p-aaaaaaaaaaaa',
      kind: 'workflow',
      state: 'running',
      lastSeq: 0,
      events: [{ seq: 0, type: 'workflow.started', data: {} }],
      result: null
    };
    const snapshots: Array<Record<string, unknown>> = [];
    let emit: (event: RunEvent) => void = () => undefined;
    const client: EngineClient = {
      async request(method, params, schema) {
        if (method !== 'run.snapshot') throw new Error(method);
        snapshots.push(params);
        if (params.sinceSeq === undefined) return schema.parse(running);
        if (params.sinceSeq === 1) {
          return schema.parse({
            ...running,
            lastSeq: 3,
            events: [
              { seq: 2, type: 'context.retrieved', data: { filePath: 'docs/nota.md' } },
              { seq: 3, type: 'agent.completed', data: { agentId: 'architecture' } }
            ]
          });
        }
        return schema.parse({ ...done, lastSeq: 5, events: [] });
      },
      async onEvent(handler) {
        emit = handler;
        return () => undefined;
      }
    };
    const event = (seq: number, type: string, data: Record<string, unknown> = {}): RunEvent => ({
      v: 1,
      kind: 'event',
      runId: 'run-1',
      seq,
      type,
      data
    });

    render(
      <AppProviders>
        <ExecutionPage runId="run-1" client={client} />
      </AppProviders>
    );
    expect(await screen.findByText('Execução iniciada')).toBeInTheDocument();

    act(() => emit(event(1, 'agent.started', { agentId: 'architecture' })));
    act(() => emit(event(1, 'agent.started', { agentId: 'architecture' })));
    expect(screen.getAllByText('Arquitetura começou')).toHaveLength(1);

    act(() => emit(event(3, 'agent.completed', { agentId: 'architecture' })));
    expect(await screen.findByText('Trecho recuperado de docs/nota.md')).toBeInTheDocument();
    expect(screen.getAllByText('Arquitetura concluiu')).toHaveLength(1);
    expect(snapshots[1]).toEqual({ runId: 'run-1', sinceSeq: 1 });

    act(() => emit(event(4, 'run.completed', { state: 'completed_with_limitations' })));
    expect(await screen.findByText('trecho visível')).toBeInTheDocument();
    expect(screen.getByText('Concluído com limitações')).toBeInTheDocument();
    expect(screen.getByText('O que aconteceu').closest('section')?.querySelectorAll('li')).toHaveLength(4);
  });

  it('liga a escuta antes do primeiro retrato e avisa quando a escuta falha', async () => {
    const order: string[] = [];
    const client: EngineClient = {
      async request(method, _params, schema) {
        order.push(method);
        return schema.parse(paused);
      },
      async onEvent() {
        order.push('listen');
        throw new Error('sem host');
      }
    };
    render(
      <AppProviders>
        <ExecutionPage runId="run-1" client={client} />
      </AppProviders>
    );

    expect(await screen.findByText('Não foi possível acompanhar os eventos desta execução.')).toBeInTheDocument();
    expect(order).toEqual(['listen', 'run.snapshot']);
  });

  it('não inventa evidência quando o resultado não traz nenhuma', async () => {
    const empty = clientFor({
      ...done,
      events: [],
      result: { ...done.result!, evidence: [], impact: undefined }
    });
    render(
      <AppProviders>
        <ExecutionPage runId="run-1" client={empty} />
      </AppProviders>
    );

    expect(await screen.findByText('Nenhuma evidência nesta execução.')).toBeInTheDocument();
    expect(screen.queryByText('Arquitetura concluiu')).not.toBeInTheDocument();
  });

  it('mostra o uso do modelo, contado pelo motor, só quando a execução usou modelo', async () => {
    const used = clientFor({
      ...done,
      result: { ...done.result!, usage: { modelCalls: 2, inputTokens: 900, outputTokens: 300 } }
    });
    const { unmount } = render(
      <AppProviders>
        <ExecutionPage runId="run-1" client={used} />
      </AppProviders>
    );

    expect(await screen.findByRole('heading', { name: 'Uso do modelo' })).toBeInTheDocument();
    expect(screen.getByText(/2 chamada\(s\) · 900 tokens de entrada · 300 de saída/)).toBeInTheDocument();
    unmount();

    render(
      <AppProviders>
        <ExecutionPage runId="run-1" client={clientFor(done)} />
      </AppProviders>
    );
    expect(await screen.findByText('Concluído com limitações')).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Uso do modelo' })).not.toBeInTheDocument();
  });

  it('mostra a opinião do juiz por modelo como registro, com notas, justificativa e o modelo, sem decidir o resultado', async () => {
    const judged = clientFor({
      ...done,
      result: {
        ...done.result!,
        evaluation: {
          verdict: 'deterministic',
          judge: 'failed',
          tokens: 790,
          judgment: {
            criteriaVersion: '1',
            scores: { plan_quality: 2, context_fit: 4, request_fit: 5 },
            rationale: 'O plano é genérico demais.',
            evidence: ['docs/nota.md#1'],
            model: 'gpt-4o-mini',
            modelVersion: 'gpt-4o-mini-2024-07-18'
          }
        }
      }
    });
    render(
      <AppProviders>
        <ExecutionPage runId="run-1" client={judged} />
      </AppProviders>
    );

    expect(await screen.findByRole('heading', { name: 'Avaliação por modelo' })).toBeInTheDocument();
    expect(screen.getByText(/Ela não decide o resultado da execução/)).toBeInTheDocument();
    expect(screen.getByText('Algum critério ficou abaixo da nota mínima.')).toBeInTheDocument();
    expect(screen.getByText('Qualidade do plano: 2 de 5 (abaixo do mínimo)')).toBeInTheDocument();
    expect(screen.getByText('Atendimento ao pedido: 5 de 5')).toBeInTheDocument();
    expect(screen.getByText('O plano é genérico demais.')).toBeInTheDocument();
    expect(screen.getByText(/gpt-4o-mini-2024-07-18/)).toBeInTheDocument();
    // O estado da execução continua o do veredito determinístico.
    expect(screen.getByText('Concluído com limitações')).toBeInTheDocument();
  });

  it('sem opinião do juiz não há seção de avaliação, e os eventos de avaliação ganham frases', async () => {
    const client = clientFor({
      ...done,
      result: {
        ...done.result!,
        evaluation: { verdict: 'deterministic', judge: 'not_run', tokens: 0 }
      },
      events: [
        { seq: 0, type: 'eval.started', data: { evalId: 'EV-JUDGE-001', kind: 'model' } },
        { seq: 1, type: 'eval.completed', data: { evalId: 'EV-JUDGE-001', kind: 'model', outcome: 'not_run' } },
        { seq: 2, type: 'eval.completed', data: { evalId: 'EV-CONFIG-001', kind: 'deterministic', outcome: 'passed' } }
      ]
    });
    render(
      <AppProviders>
        <ExecutionPage runId="run-1" client={client} />
      </AppProviders>
    );

    expect(await screen.findByText('Avaliando o resultado com o juiz por modelo')).toBeInTheDocument();
    expect(screen.getByText('O juiz por modelo não rodou')).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Avaliação por modelo' })).not.toBeInTheDocument();
  });

  const proposing: WorkflowSnapshot = {
    ...paused,
    events: [
      ...paused.events,
      {
        seq: 2,
        type: 'approval.required',
        data: {
          approvalId: 'apr-0123456789ab',
          executionId: 'ex-0123456789ab',
          actions: [
            {
              id: 'act-decisao-impacto',
              kind: 'file.write',
              summary: 'Registrar a decisão de arquitetura',
              target: '.mariliz/decisions/ex-0123456789ab-impacto.md'
            }
          ]
        }
      }
    ]
  };

  it('lista as ações propostas desmarcadas e aprovar sem marcar não manda nenhuma ação ao motor', async () => {
    const client = clientFor(proposing, done);
    render(
      <AppProviders>
        <ExecutionPage runId="run-1" client={client} />
      </AppProviders>
    );

    const action = await screen.findByRole('checkbox', { name: /Registrar a decisão de arquitetura/ });
    expect(action).not.toBeChecked();
    expect(screen.getByText('.mariliz/decisions/ex-0123456789ab-impacto.md')).toBeInTheDocument();
    expect(screen.getByText(/Nada é gravado no projeto sem a sua aprovação/)).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Aprovar sem executar ações' }));

    expect(client.resumes).toEqual([{ projectId: 'p-aaaaaaaaaaaa', runId: 'run-1' }]);
  });

  it('marcar a ação manda o identificador da aprovação e a ação, e o botão diz o que vai acontecer', async () => {
    const client = clientFor(proposing, done);
    render(
      <AppProviders>
        <ExecutionPage runId="run-1" client={client} />
      </AppProviders>
    );

    await userEvent.click(await screen.findByRole('checkbox', { name: /Registrar a decisão de arquitetura/ }));
    await userEvent.click(screen.getByRole('button', { name: 'Aprovar e executar as ações marcadas' }));

    expect(client.resumes).toEqual([
      {
        projectId: 'p-aaaaaaaaaaaa',
        runId: 'run-1',
        approvalId: 'apr-0123456789ab',
        actionIds: ['act-decisao-impacto']
      }
    ]);
  });

  it('depois de concluir mostra o que aconteceu com cada ação, como o motor registrou', async () => {
    const finished = clientFor({
      ...done,
      result: {
        ...done.result!,
        actions: [
          {
            id: 'act-decisao-impacto',
            kind: 'file.write',
            summary: 'Registrar a decisão de arquitetura',
            target: '.mariliz/decisions/ex-0123456789ab-impacto.md',
            status: 'conflict'
          }
        ]
      }
    });
    render(
      <AppProviders>
        <ExecutionPage runId="run-1" client={finished} />
      </AppProviders>
    );

    expect(await screen.findByRole('heading', { name: 'Ações' })).toBeInTheDocument();
    expect(screen.getByText(/já existe um arquivo diferente neste caminho/)).toBeInTheDocument();
  });

  it('mostra a falha da execução (o resultado de falha não traz resumo) e a tela passa no axe', async () => {
    // O schema precisa aceitar o resultado de falha como o motor o envia: `parse` falha se o resumo voltar a ser obrigatório.
    const failed = workflowSnapshotSchema.parse({
      ...paused,
      state: 'failed',
      events: [
        { seq: 0, type: 'workflow.started', data: {} },
        { seq: 1, type: 'agent.started', data: { agentId: 'planning', agentVersion: '1' } }
      ],
      // Como o motor grava a falha: sem `summary`, só o código e a mensagem.
      result: { kind: 'workflow', error: { code: 'AI_RUNTIME_UNAVAILABLE', message: 'O runtime de IA não respondeu.' } }
    });
    const { container } = render(
      <AppProviders>
        <ExecutionPage runId="run-1" client={clientFor(failed)} />
      </AppProviders>
    );

    expect(await screen.findByText('A execução falhou')).toBeInTheDocument();
    expect(screen.getByText('O runtime de IA não respondeu.')).toBeInTheDocument();
    expect(screen.getByText(/Você pode iniciar o pedido de novo/)).toBeInTheDocument();
    expect(screen.getByText('AI_RUNTIME_UNAVAILABLE')).toBeInTheDocument();
    // A etapa que estava em andamento fica marcada como falha, não como concluída.
    expect(screen.getByText('Planejamento').closest('li')).toHaveTextContent('Falhou');
    await expectNoA11yViolations(container);
  });

  it('não inventa nada: sem eventos e sem resultado só há o estado', async () => {
    const bare = clientFor({ ...paused, state: 'running', events: [], result: null });
    render(
      <AppProviders>
        <ExecutionPage runId="run-1" client={bare} />
      </AppProviders>
    );

    expect(await screen.findByText('Execução')).toBeInTheDocument();
    for (const heading of [
      'Etapas',
      'O que aconteceu',
      'Evidências',
      'Impacto técnico',
      'Limitações',
      'Avaliação por modelo'
    ]) {
      expect(screen.queryByRole('heading', { name: heading })).not.toBeInTheDocument();
    }
    expect(screen.queryByText('Nenhuma evidência nesta execução.')).not.toBeInTheDocument();
  });

  it('mostra as etapas que os eventos anunciaram e os trechos com origem, mesmo antes do resultado', async () => {
    const hash = 'a'.repeat(64);
    const live = clientFor({
      ...paused,
      state: 'running',
      events: [
        { seq: 0, type: 'context.retrieval.started', data: {} },
        {
          seq: 1,
          type: 'context.retrieved',
          data: { chunkId: 'docs/nota.md#1', filePath: 'docs/nota.md', hash, score: 2.345 }
        },
        { seq: 2, type: 'agent.started', data: { agentId: 'planning', agentVersion: '1' } }
      ],
      result: null
    });
    render(
      <AppProviders>
        <ExecutionPage runId="run-1" client={live} />
      </AppProviders>
    );

    expect(await screen.findByRole('heading', { name: 'Etapas' })).toBeInTheDocument();
    expect(screen.getByText('Contexto do projeto').closest('li')).toHaveTextContent('1 trecho(s) recuperado(s)');
    expect(screen.getByText('Planejamento').closest('li')).toHaveTextContent('Em andamento · Agente, versão 1');
    const evidence = screen.getByText('docs/nota.md').closest('li')!;
    expect(evidence).toHaveTextContent('Trecho docs/nota.md#1 · pontuação 2.35 · versão aaaaaaaaaaaa');
    expect(screen.getByText('O texto de cada trecho aparece quando a execução termina.')).toBeInTheDocument();
    // Nenhum evento de aprovação, avaliação ou fim: nada disso aparece.
    expect(screen.queryByText('Aprovação')).not.toBeInTheDocument();
    expect(screen.queryByText('Avaliação')).not.toBeInTheDocument();
    expect(screen.queryByText('Resultado')).not.toBeInTheDocument();
  });

  it('com o resultado, a evidência traz a origem conferida e o risco do impacto tem texto próprio', async () => {
    const finished = clientFor({
      ...done,
      events: [],
      result: {
        ...done.result!,
        evidence: [
          {
            filePath: 'docs/nota.md',
            excerpt: 'trecho visível',
            chunkId: 'docs/nota.md#1',
            hash: 'b'.repeat(64),
            score: 1.25
          }
        ],
        impact: {
          schema: 'engineering.impact.v1',
          areas: ['servico'],
          risks: ['sem-stack'],
          evidence: ['docs/nota.md#1']
        }
      }
    });
    const { container } = render(
      <AppProviders>
        <ExecutionPage runId="run-1" client={finished} />
      </AppProviders>
    );

    expect(await screen.findByText('Nenhuma stack foi identificada no projeto.')).toBeInTheDocument();
    expect(screen.getByText('docs/nota.md').closest('li')).toHaveTextContent(
      'Trecho docs/nota.md#1 · pontuação 1.25 · versão bbbbbbbbbbbb · citado no impacto técnico'
    );
    await expectNoA11yViolations(container);
  });

  it('a tela completa (etapas, avaliação, ações e uso) passa no axe', async () => {
    const full = clientFor({
      ...proposing,
      events: [
        ...proposing.events,
        { seq: 3, type: 'eval.completed', data: { evalId: 'EV-JUDGE-001', kind: 'model', outcome: 'passed' } }
      ],
      result: null
    });
    const { container } = render(
      <AppProviders>
        <ExecutionPage runId="run-1" client={full} />
      </AppProviders>
    );

    expect(await screen.findByRole('checkbox', { name: /Registrar a decisão de arquitetura/ })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Etapas' })).toBeInTheDocument();
    expect(screen.getByText('Aprovação').closest('li')).toHaveTextContent('Esperando a sua decisão');
    await expectNoA11yViolations(container);
  });

  it('o Planejamento aparece com o nome certo na linha do tempo', async () => {
    const client = clientFor({
      ...done,
      events: [
        { seq: 0, type: 'agent.started', data: { agentId: 'planning' } },
        { seq: 1, type: 'agent.completed', data: { agentId: 'planning' } }
      ]
    });
    render(
      <AppProviders>
        <ExecutionPage runId="run-1" client={client} />
      </AppProviders>
    );

    expect(await screen.findByText('Planejamento começou')).toBeInTheDocument();
    expect(screen.getByText('Planejamento concluiu')).toBeInTheDocument();
  });
});
