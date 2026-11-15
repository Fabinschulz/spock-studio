import connectionsFixture from '@contracts/fixtures/cloud/connections.json';
import linksFixture from '@contracts/fixtures/cloud/links.json';
import bootstrapFixture from '@contracts/fixtures/iac/bootstrap-plan.json';
import layoutFixture from '@contracts/fixtures/iac/layout.json';
import offerFixture from '@contracts/fixtures/iac/offer.json';
import operationsFixture from '@contracts/fixtures/iac/operations.json';
import stateResourcesFixture from '@contracts/fixtures/iac/state-resources.json';
import stateFixture from '@contracts/fixtures/iac/state.json';
import statusFixture from '@contracts/fixtures/iac/status.json';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router';
import { describe, expect, it } from 'vitest';

import type { EngineClient } from '@/shared/api';
import { AppProviders } from '@/shared/components';
import { expectNoA11yViolations } from '@/test/a11y';

import { InfrastructurePage } from './infrastructure-page';

const demo = { projectId: 'p-0123456789ab', displayName: 'loja-api', displayPath: '~\\loja-api' };
const ready = statusFixture.runtime;
const missing = {
  ...ready,
  state: 'missing',
  installations: [],
  selected: null,
  project: { ...ready.project, selected: null, automatic: false },
  message:
    'Nenhuma ferramenta de infraestrutura como código instalada. O Spock pode instalar o OpenTofu, gratuito, com a sua autorização.'
};

function fakeEngine(initial: Record<string, unknown> = missing) {
  let current: Record<string, unknown> = initial;
  const calls: { method: string; params: Record<string, unknown> }[] = [];
  const client: EngineClient = {
    async request(method, params, schema) {
      calls.push({ method, params: params as Record<string, unknown> });
      if (method === 'iac.runtime.install') current = ready;
      if (method === 'iac.runtime.decline')
        current = {
          ...missing,
          state: 'declined',
          declinedAt: '2026-11-15T13:00:00+00:00',
          message: 'Instalação recusada.'
        };
      const reply: Record<string, unknown> = {
        'iac.runtime.status': { runtime: current },
        'iac.runtime.install': { runtime: current },
        'iac.runtime.decline': { runtime: current },
        'iac.runtime.pin': { runtime: current },
        'iac.runtime.offer': offerFixture,
        'iac.layout': layoutFixture,
        'iac.operations': operationsFixture,
        'iac.validate': { operation: operationsFixture.operations[3] },
        'iac.plan': { operation: operationsFixture.operations[0] },
        'iac.apply': { operation: operationsFixture.operations[1] },
        'iac.state': stateFixture,
        'iac.state.resources': stateResourcesFixture,
        'iac.state.unlock': { unlocked: true },
        'iac.bootstrap.plan': bootstrapFixture,
        'iac.bootstrap.save': { runId: 'run-iac-1' },
        'cloud.list': connectionsFixture,
        'cloud.links': { links: linksFixture.links.map((link) => ({ ...link, projectId: demo.projectId })) }
      };
      return schema.parse(reply[method]);
    },
    async onEvent() {
      return () => undefined;
    }
  };
  return { client, calls };
}

function renderPage(client: EngineClient, project: typeof demo | null = demo) {
  return render(
    <AppProviders>
      <MemoryRouter>
        <InfrastructurePage client={client} project={project} />
      </MemoryRouter>
    </AppProviders>
  );
}

const methods = (calls: { method: string }[]) => calls.map((call) => call.method);

describe('Infraestrutura', () => {
  it('explica a instalação do OpenTofu gratuito e não baixa nada antes da autorização', async () => {
    const engine = fakeEngine();
    const { container } = renderPage(engine.client);

    expect(await screen.findByText(/Nenhuma instalação será realizada sem sua autorização/)).toBeInTheDocument();
    expect(screen.getByText(/OpenTofu nesta máquina: ferramenta gratuita e de código aberto/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Autorizar instalação' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Não instalar agora' })).toBeInTheDocument();
    expect(methods(engine.calls)).not.toContain('iac.runtime.offer');
    expect(methods(engine.calls)).not.toContain('iac.runtime.install');
    await expectNoA11yViolations(container);
  });

  it('autorizar mostra a proposta verificável do OpenTofu e instala só a proposta mostrada', async () => {
    const engine = fakeEngine();
    renderPage(engine.client);

    await userEvent.click(await screen.findByRole('button', { name: 'Autorizar instalação' }));
    expect(await screen.findByText(offerFixture.offer.archiveUrl)).toBeInTheDocument();
    expect(screen.getByText(/assinada por quem publica o OpenTofu/)).toBeInTheDocument();
    expect(screen.getByText(offerFixture.offer.license)).toBeInTheDocument();
    expect(engine.calls.find((call) => call.method === 'iac.runtime.offer')?.params).not.toHaveProperty('engine');
    expect(methods(engine.calls)).not.toContain('iac.runtime.install');

    await userEvent.click(screen.getByRole('button', { name: `Instalar OpenTofu ${offerFixture.offer.version}` }));

    expect(
      await screen.findByText(`OpenTofu ${offerFixture.offer.version} instalado e verificado.`)
    ).toBeInTheDocument();
    expect(engine.calls.find((call) => call.method === 'iac.runtime.install')?.params).toEqual({
      offerId: offerFixture.offer.offerId,
      version: offerFixture.offer.version,
      engine: 'openTofu'
    });
  });

  it('o Terraform da HashiCorp continua disponível como escolha explícita', async () => {
    const engine = fakeEngine();
    renderPage(engine.client);

    await userEvent.click(await screen.findByRole('button', { name: 'Prefiro o Terraform da HashiCorp' }));

    expect(engine.calls.find((call) => call.method === 'iac.runtime.offer')?.params).toMatchObject({
      engine: 'terraform'
    });
  });

  it('recusar registra a decisão e não pede proposta', async () => {
    const engine = fakeEngine();
    renderPage(engine.client);

    await userEvent.click(await screen.findByRole('button', { name: 'Não instalar agora' }));

    expect(await screen.findByText(/nada será instalado/)).toBeInTheDocument();
    expect(screen.getByText('Instalação recusada')).toBeInTheDocument();
    expect(methods(engine.calls)).not.toContain('iac.runtime.offer');
  });

  it('mostra o que o projeto exige, usa o OpenTofu por padrão e fixa a ferramenta escolhida', async () => {
    const engine = fakeEngine(ready);
    renderPage(engine.client);

    expect(await screen.findByText('infra/providers.tf')).toBeInTheDocument();
    expect(screen.getByText(/OpenTofu 1\.13\.9 · automático/)).toBeInTheDocument();
    expect(screen.getByText(/^Gerenciado pelo Spock ·/)).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Fixar Terraform 1.16.5 (externo)' }));

    expect(engine.calls.find((call) => call.method === 'iac.runtime.pin')?.params).toEqual({
      projectId: demo.projectId,
      engine: 'terraform',
      version: '1.16.5',
      source: 'external'
    });
  });

  it('separa as etapas que só analisam da que muda a nuvem', async () => {
    renderPage(fakeEngine(ready).client);

    expect(await screen.findByRole('list', { name: 'Ciclo de uma mudança de infraestrutura' })).toBeInTheDocument();
    expect(screen.getAllByText('Só analisa')).toHaveLength(6);
    expect(screen.getAllByText('Disponível')).toHaveLength(6);
    expect(screen.getAllByText('Próxima entrega')).toHaveLength(2);
    expect(screen.getAllByText('Muda a nuvem')).toHaveLength(1);
    expect(screen.getByText('Prepara a mudança')).toBeInTheDocument();
  });

  it('validar e planejar mostram o resultado e passam pasta, ambiente e conexão escolhidos', async () => {
    const engine = fakeEngine(ready);
    renderPage(engine.client);

    expect(await screen.findByText(/não mudam a nuvem nem o projeto/)).toBeInTheDocument();
    const plan = operationsFixture.operations[0]!;
    const summary = await screen.findByLabelText('Resumo do plano');
    expect(within(summary).getByText(`Destruir ${plan.summary!.delete}`)).toBeInTheDocument();
    expect(
      within(screen.getByLabelText('Mudanças do plano')).getByText('module.rede.terraform_data.rede')
    ).toBeInTheDocument();

    await userEvent.click(screen.getByRole('radio', { name: 'infra/modules/rede' }));
    await userEvent.type(screen.getByLabelText(/Ambiente/), 'dev');
    const connection = connectionsFixture.connections.find((c) => c.id === linksFixture.links[0]!.connectionId)!;
    await userEvent.click(screen.getByRole('radio', { name: `Conexão ${connection.name} (${connection.provider})` }));
    await userEvent.click(screen.getByRole('button', { name: 'Gerar plano' }));

    expect(await screen.findByText('Plano gerado e guardado.')).toBeInTheDocument();
    expect(engine.calls.find((call) => call.method === 'iac.plan')?.params).toEqual({
      projectId: demo.projectId,
      folder: 'infra/modules/rede',
      workspace: 'dev',
      connectionId: connection.id
    });

    await userEvent.click(screen.getByRole('button', { name: 'Validar' }));
    expect(await screen.findByText('Configuração válida e formatada.')).toBeInTheDocument();
    expect(methods(engine.calls)).not.toContain('iac.apply');
  });

  it('sem ferramenta pronta, validar e planejar ficam desligados', async () => {
    renderPage(fakeEngine().client);

    expect(
      await screen.findByText('Instale ou escolha a ferramenta acima para validar e planejar.')
    ).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Validar' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Gerar plano' })).toBeDisabled();
  });

  it('aprovar exige confirmar a destruição e passa o planId mostrado', async () => {
    const engine = fakeEngine(ready);
    renderPage(engine.client);
    const plan = operationsFixture.operations[0]!;

    const approval = await screen.findByLabelText(`Aprovar o plano ${plan.planId}`);
    const button = within(approval).getByRole('button', { name: 'Aprovar e aplicar' });
    expect(button).toBeDisabled();
    expect(within(approval).getByText(/muda a nuvem/)).toBeInTheDocument();

    await userEvent.click(
      within(approval).getByRole('checkbox', { name: /Confirmo destruir ou substituir 1 recurso/ })
    );
    await userEvent.click(button);
    expect(methods(engine.calls)).not.toContain('iac.apply');
    await userEvent.click(within(approval).getByRole('button', { name: 'Aplicar agora' }));

    expect(
      await screen.findByText('Plano aplicado e verificado: o plano seguinte não tem mudanças.')
    ).toBeInTheDocument();
    expect(engine.calls.find((call) => call.method === 'iac.apply')?.params).toEqual({
      projectId: demo.projectId,
      operationId: plan.id,
      planId: plan.planId,
      destroyConfirmed: true,
      risksAccepted: false
    });
  });

  it('aplicação mostra a verificação, o state e os outputs; plano já aplicado não pode ser aprovado de novo', async () => {
    renderPage(fakeEngine(ready).client);
    const apply = operationsFixture.operations[1]!;
    const applied = operationsFixture.operations[2]!;

    await userEvent.click(await screen.findByRole('button', { name: /^Aplicação de / }));
    expect(screen.getByText('Aplicado e verificado')).toBeInTheDocument();
    expect(screen.getByText(/O state tem 2 recurso/)).toBeInTheDocument();
    expect(within(screen.getByLabelText('Outputs')).getByText(apply.apply!.outputs.fila!)).toBeInTheDocument();

    const planButtons = screen.getAllByRole('button', { name: /^Plano de / });
    await userEvent.click(planButtons[planButtons.length - 1]!);
    expect(screen.getByText(`Este plano já foi aplicado (${applied.appliedBy}).`)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Aprovar e aplicar' })).not.toBeInTheDocument();
  });

  it('state mostra onde vive cada raiz e consulta só endereços', async () => {
    const engine = fakeEngine(ready);
    renderPage(engine.client);

    const roots = await screen.findByLabelText('Raízes e onde vive o state');
    expect(within(roots).getByText('Remoto')).toBeInTheDocument();
    expect(within(roots).getByText('Local, no projeto')).toBeInTheDocument();
    expect(within(roots).getByText(/Bootstrap com state local, como no modelo/)).toBeInTheDocument();

    await userEvent.click(within(roots).getAllByRole('button', { name: 'Consultar o state' })[0]!);

    const read = await screen.findByLabelText('State de infra');
    for (const address of stateResourcesFixture.state.addresses)
      expect(within(read).getByText(address)).toBeInTheDocument();
    expect(engine.calls.find((call) => call.method === 'iac.state.resources')?.params).toEqual({
      projectId: demo.projectId,
      folder: 'infra'
    });
  });

  it('bootstrap mostra os arquivos e o bloco backend antes de gravar e grava só com o planId revisado', async () => {
    const engine = fakeEngine(ready);
    renderPage(engine.client);

    const form = await screen.findByLabelText('Bootstrap do state');
    await userEvent.click(within(form).getByRole('radio', { name: 'AWS (S3)' }));
    await userEvent.type(within(form).getByLabelText('Bucket de state'), 'loja-terraform-state-123456789012-sa-east-1');
    await userEvent.click(within(form).getByRole('button', { name: 'Revisar os arquivos' }));

    const review = await screen.findByLabelText('Revisar o bootstrap');
    expect(within(review).getByLabelText('Diferenças em infra/bootstrap/resource.tf')).toHaveTextContent(
      'prevent_destroy = true'
    );
    expect(within(review).getByLabelText('Diferenças em bloco backend')).toHaveTextContent('use_lockfile');
    expect(methods(engine.calls)).not.toContain('iac.bootstrap.save');

    await userEvent.click(within(review).getByRole('button', { name: 'Gravar no projeto' }));
    await userEvent.click(within(review).getByRole('button', { name: 'Gravar' }));

    expect(await screen.findByText(/Arquivos do bootstrap gravados na execução run-iac-1/)).toBeInTheDocument();
    expect(engine.calls.find((call) => call.method === 'iac.bootstrap.save')?.params).toMatchObject({
      projectId: demo.projectId,
      provider: 'aws',
      planId: bootstrapFixture.plan.planId,
      values: { bucket: 'loja-terraform-state-123456789012-sa-east-1' }
    });
  });
});
