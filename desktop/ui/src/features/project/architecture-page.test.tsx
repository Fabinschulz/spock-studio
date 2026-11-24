import architectureFixture from '@contracts/fixtures/engineering/architecture.json';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router';
import { describe, expect, it, vi } from 'vitest';

import type { EngineClient } from '@/shared/api';
import { AppProviders } from '@/shared/components';
import { expectNoA11yViolations } from '@/test/a11y';

import { ArchitecturePage } from './architecture-page';

const project = { projectId: 'p-000000000001', displayName: 'Svc', displayPath: '~\\Svc' };

function clientWith(...replies: unknown[]) {
  const queue = [...replies];
  const request = vi.fn(async (method: string, _params: unknown, schema: { parse(value: unknown): unknown }) => {
    if (method !== 'project.architecture') throw new Error(`método inesperado: ${method}`);
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
    const { client, request } = clientWith(withoutAssessment);
    const { container } = renderPage(client);

    const violations = await screen.findByLabelText('Violações de direção');
    expect(violations).toHaveTextContent('Svc.Application (aplicação) depende de Svc.Infrastructure (infraestrutura)');
    expect(violations).toHaveTextContent('[dependency-direction]');
    expect((screen.getByLabelText('Pedido para o agente') as HTMLTextAreaElement).value).toContain(
      'spock_architecture_assessment_prepare'
    );
    expect(screen.getByText('.spock/architecture-assessment.json')).toBeInTheDocument();
    expect(screen.queryByLabelText('Avaliação gravada')).not.toBeInTheDocument();
    expect(request).toHaveBeenCalledWith('project.architecture', { projectId: project.projectId }, expect.anything());
    await expectNoA11yViolations(container);
  });

  it('a avaliação que o agente gravou aparece por classificação, com arquivos, referências e próximo passo', async () => {
    const { client } = clientWith(architectureFixture);
    renderPage(client);

    const assessment = await screen.findByLabelText('Avaliação gravada');
    expect(assessment).toHaveTextContent('Conferida pelo Spock');
    const risks = within(assessment).getByLabelText('Divergências de risco');
    expect(risks).toHaveTextContent('Aplicação referencia a infraestrutura');
    expect(risks).toHaveTextContent('Próximo passo: Criar a interface do repositório');
    expect(risks).toHaveTextContent('Svc.Application/Pedidos/CriarPedido.cs:2 · REF-N1 · [dependency-direction]');
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
    const { client, request } = clientWith(broken, architectureFixture);
    renderPage(client);

    const checks = await screen.findByLabelText('Verificações da avaliação');
    expect(checks).toHaveTextContent('Toda referência citada existe na pasta Organização (REF-Z99)');
    expect(screen.getByText('1 verificação(ões) falhando')).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Ler de novo' }));
    expect(await screen.findByText('Conferida pelo Spock')).toBeInTheDocument();
    expect(request).toHaveBeenCalledTimes(2);
  });
});
