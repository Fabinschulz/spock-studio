import architectureFixture from '@contracts/fixtures/engineering/architecture.json';
import modernizationFixture from '@contracts/fixtures/engineering/modernization.json';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router';
import { describe, expect, it, vi } from 'vitest';

import type { EngineClient } from '@/shared/api';
import { AppProviders } from '@/shared/components';
import { expectNoA11yViolations } from '@/test/a11y';

import { ArchitecturePage } from './architecture-page';

const project = { projectId: 'p-000000000001', displayName: 'Svc', displayPath: '~\\Svc' };

const withoutPlan = {
  modernization: { ...modernizationFixture.modernization, plan: null, steps: [] }
};

/** Cada método tem a própria fila de respostas; a última se repete. */
function clientWith(architecture: unknown[], modernization: unknown[] = [withoutPlan]) {
  const queues: Record<string, unknown[]> = {
    'project.architecture': [...architecture],
    'modernization.plan': [...modernization],
    'modernization.decide': [...modernization]
  };
  const request = vi.fn(async (method: string, _params: unknown, schema: { parse(value: unknown): unknown }) => {
    const queue = queues[method];
    if (!queue) throw new Error(`método inesperado: ${method}`);
    return schema.parse(queue.length > 1 ? queue.shift() : queue[0]);
  });
  return { client: { request, onEvent: vi.fn() } as unknown as EngineClient, request };
}

const renderPage = (client: EngineClient) =>
  render(
    <AppProviders>
      <MemoryRouter>
        <ArchitecturePage client={client} project={project} />
      </MemoryRouter>
    </AppProviders>
  );

const withoutAssessment = {
  architecture: { ...architectureFixture.architecture, assessment: null }
};

describe('Arquitetura do projeto', () => {
  it('mostra a análise do Spock e o pedido para o agente enquanto não há avaliação', async () => {
    const { client, request } = clientWith([withoutAssessment]);
    const { container } = renderPage(client);

    const violations = await screen.findByLabelText('Violações de direção');
    expect(violations).toHaveTextContent('Svc.Application (aplicação) depende de Svc.Infrastructure (infraestrutura)');
    expect(violations).toHaveTextContent('[dependency-direction]');
    expect((screen.getAllByLabelText('Pedido para o agente')[0] as HTMLTextAreaElement).value).toContain(
      'spock_architecture_assessment_prepare'
    );
    expect(screen.getByText('.spock/architecture-assessment.json')).toBeInTheDocument();
    expect(screen.queryByLabelText('Avaliação gravada')).not.toBeInTheDocument();
    expect(request).toHaveBeenCalledWith('project.architecture', { projectId: project.projectId }, expect.anything());
    await expectNoA11yViolations(container);
  });

  it('a avaliação que o agente gravou aparece por classificação, com arquivos, referências e próximo passo', async () => {
    const { client } = clientWith([architectureFixture]);
    renderPage(client);

    const assessment = await screen.findByLabelText('Avaliação gravada');
    expect(assessment).toHaveTextContent('Conferida pelo Spock');
    const risks = within(assessment).getByLabelText('Divergências de risco');
    expect(risks).toHaveTextContent('Aplicação referencia a infraestrutura');
    expect(risks).toHaveTextContent('Próximo passo: Criar a interface do repositório');
    expect(risks).toHaveTextContent('Svc.Application/Pedidos/CriarPedido.cs:2 · REF-N1 · [dependency-direction]');
    expect(risks).toHaveTextContent('D1 · Aplicação referencia a infraestrutura');
    expect(within(assessment).queryByLabelText('Divergências de custo')).not.toBeInTheDocument();
    expect(within(assessment).getByLabelText('Convenções do projeto')).toHaveTextContent('Um projeto por camada');
  });

  it('o que o Spock não conseguiu conferir aparece antes das divergências, e ler de novo pega a versão nova', async () => {
    const failing = structuredClone(architectureFixture);
    const failingChecks = failing.architecture.assessment.checks.map((check) =>
      check.id === 'references-exist' ? { ...check, passed: false, detail: 'REF-Z99' } : check
    );
    const broken = {
      architecture: {
        ...failing.architecture,
        assessment: { ...failing.architecture.assessment, checks: failingChecks, valid: false }
      }
    };
    const { client, request } = clientWith([broken, architectureFixture]);
    renderPage(client);

    const checks = await screen.findByLabelText('Verificações da avaliação');
    expect(checks).toHaveTextContent('Toda referência citada existe na pasta Organização (REF-Z99)');
    expect(screen.getByText('1 verificação(ões) falhando')).toBeInTheDocument();

    await userEvent.click(screen.getAllByRole('button', { name: 'Ler de novo' })[0]!);
    expect(await screen.findByText('Conferida pelo Spock')).toBeInTheDocument();
    expect(request.mock.calls.filter(([method]) => method === 'project.architecture')).toHaveLength(2);
  });
});

describe('Plano de modernização', () => {
  const approvedBoth = {
    modernization: {
      ...modernizationFixture.modernization,
      steps: modernizationFixture.modernization.steps.map((step) => ({
        ...step,
        status: 'approved',
        decisionId: 'dec-000000000002',
        recordedAt: step.recordedAt ?? '2026-11-24T12:05:00+00:00'
      }))
    }
  };

  it('sem plano, mostra o pedido para o agente e onde ele grava', async () => {
    const { client } = clientWith([architectureFixture]);
    renderPage(client);

    expect(await screen.findByText('.spock/modernization-plan.json')).toBeInTheDocument();
    const prompts = screen.getAllByLabelText('Pedido para o agente') as HTMLTextAreaElement[];
    expect(prompts.map((prompt) => prompt.value).join('\n')).toContain('spock_modernization_plan_prepare');
    expect(screen.queryByLabelText('Plano gravado')).not.toBeInTheDocument();
  });

  it('mostra a caracterização e cada passo com a divergência que trata, como se desfaz e a decisão', async () => {
    const { client } = clientWith([architectureFixture], [modernizationFixture]);
    const { container } = renderPage(client);

    const plan = await screen.findByLabelText('Plano gravado');
    expect(plan).toHaveTextContent('Conferido pelo Spock');
    expect(within(plan).getByLabelText('Caracterização')).toHaveTextContent('POST /pedidos cria o pedido');
    const first = within(plan).getByLabelText('Passo S1');
    expect(first).toHaveTextContent('Aprovado');
    expect(first).toHaveTextContent('Trata: D2 Sem testes');
    expect(first).toHaveTextContent('Desfazer: Apagar o projeto Svc.Tests.');
    const second = within(plan).getByLabelText('Passo S2');
    expect(second).toHaveTextContent('Aguardando você');
    expect(second).toHaveTextContent('depende de S1');
    await expectNoA11yViolations(container);
  });

  it('aprovar um passo manda a decisão com o motivo e mostra o novo estado', async () => {
    const { client, request } = clientWith([architectureFixture], [modernizationFixture, approvedBoth]);
    renderPage(client);

    const second = await screen.findByLabelText('Passo S2');
    await userEvent.type(within(second).getByLabelText('Motivo da decisão de S2 (opcional)'), 'Teste de S1 já passa.');
    await userEvent.click(within(second).getByRole('button', { name: 'Aprovar S2' }));

    expect(request).toHaveBeenCalledWith(
      'modernization.decide',
      { projectId: project.projectId, stepId: 'S2', decision: 'approved', rationale: 'Teste de S1 já passa.' },
      expect.anything()
    );
    expect(await screen.findByText('S2 aprovado e registrado nas decisões do projeto.')).toBeInTheDocument();
  });

  it('plano que não passa nas verificações mostra o que falha e não deixa decidir', async () => {
    const failing = {
      modernization: {
        ...modernizationFixture.modernization,
        plan: {
          ...modernizationFixture.modernization.plan,
          valid: false,
          checks: modernizationFixture.modernization.plan.checks.map((check) =>
            check.id === 'rewrite-plan' ? { ...check, passed: false, detail: 'S2' } : check
          )
        }
      }
    };
    const { client } = clientWith([architectureFixture], [failing]);
    renderPage(client);

    const checks = await screen.findByLabelText('Verificações do plano');
    expect(checks).toHaveTextContent('Toda reescrita traz custo, risco e plano de convivência (S2)');
    expect(screen.queryByRole('button', { name: 'Aprovar S2' })).not.toBeInTheDocument();
  });
});
