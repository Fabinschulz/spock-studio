import connectionsFixture from '@contracts/fixtures/cloud/connections.json';
import offerFixture from '@contracts/fixtures/iac/offer.json';
import statusFixture from '@contracts/fixtures/iac/status.json';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router';
import { describe, expect, it } from 'vitest';

import type { EngineClient } from '@/shared/api';
import { AppProviders } from '@/shared/components';
import { expectNoA11yViolations } from '@/test/a11y';

import { InfrastructurePage } from './infrastructure-page';

const demo = { projectId: 'p-0123456789ab', displayName: 'loja-api', displayPath: '~\\loja-api' };
const ready = statusFixture.terraform;
const missing = {
  ...ready,
  state: 'missing',
  installations: [],
  selected: null,
  project: { ...ready.project, selected: null, automatic: false },
  message: 'O Terraform não está instalado. O Spock pode instalá-lo com a sua autorização.'
};

function fakeEngine(initial: Record<string, unknown> = missing) {
  let current: Record<string, unknown> = initial;
  const calls: { method: string; params: Record<string, unknown> }[] = [];
  const client: EngineClient = {
    async request(method, params, schema) {
      calls.push({ method, params: params as Record<string, unknown> });
      if (method === 'iac.terraform.install') current = ready;
      if (method === 'iac.terraform.decline')
        current = {
          ...missing,
          state: 'declined',
          declinedAt: '2026-11-15T13:00:00+00:00',
          message: 'Instalação recusada.'
        };
      const reply: Record<string, unknown> = {
        'iac.terraform.status': { terraform: current },
        'iac.terraform.install': { terraform: current },
        'iac.terraform.decline': { terraform: current },
        'iac.terraform.pin': { terraform: current },
        'iac.terraform.offer': offerFixture,
        'cloud.list': connectionsFixture
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

describe('Infraestrutura', () => {
  it('explica a instalação e não baixa nada antes da autorização', async () => {
    const engine = fakeEngine();
    const { container } = renderPage(engine.client);

    expect(await screen.findByText(/Nenhuma instalação será realizada sem sua autorização/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Autorizar instalação' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Não instalar agora' })).toBeInTheDocument();
    expect(engine.calls.map((call) => call.method)).not.toContain('iac.terraform.offer');
    expect(engine.calls.map((call) => call.method)).not.toContain('iac.terraform.install');
    await expectNoA11yViolations(container);
  });

  it('autorizar mostra a proposta verificável e instala só a proposta mostrada', async () => {
    const engine = fakeEngine();
    renderPage(engine.client);

    await userEvent.click(await screen.findByRole('button', { name: 'Autorizar instalação' }));
    expect(await screen.findByText(offerFixture.offer.archiveUrl)).toBeInTheDocument();
    expect(screen.getByText(/assinada pela HashiCorp/)).toBeInTheDocument();
    expect(engine.calls.map((call) => call.method)).not.toContain('iac.terraform.install');

    await userEvent.click(screen.getByRole('button', { name: `Instalar Terraform ${offerFixture.offer.version}` }));

    expect(
      await screen.findByText(`Terraform ${offerFixture.offer.version} instalado e verificado.`)
    ).toBeInTheDocument();
    expect(engine.calls.find((call) => call.method === 'iac.terraform.install')?.params).toEqual({
      offerId: offerFixture.offer.offerId,
      version: offerFixture.offer.version
    });
  });

  it('recusar registra a decisão e não pede proposta', async () => {
    const engine = fakeEngine();
    renderPage(engine.client);

    await userEvent.click(await screen.findByRole('button', { name: 'Não instalar agora' }));

    expect(await screen.findByText(/nada será instalado/)).toBeInTheDocument();
    expect(screen.getByText('Instalação recusada')).toBeInTheDocument();
    expect(engine.calls.map((call) => call.method)).not.toContain('iac.terraform.offer');
  });

  it('mostra o que o projeto exige e fixa a versão escolhida', async () => {
    const engine = fakeEngine(ready);
    renderPage(engine.client);

    expect(await screen.findByText('infra/providers.tf')).toBeInTheDocument();
    expect(screen.getByText(/Terraform 1\.17\.2 · automático/)).toBeInTheDocument();
    expect(screen.getByText(/^Gerenciado pelo Spock ·/)).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Fixar 1.16.5 (externo)' }));

    expect(engine.calls.find((call) => call.method === 'iac.terraform.pin')?.params).toEqual({
      projectId: demo.projectId,
      version: '1.16.5',
      source: 'external'
    });
  });

  it('separa as etapas que só analisam da que muda a nuvem', async () => {
    renderPage(fakeEngine(ready).client);

    expect(await screen.findByRole('list', { name: 'Ciclo de uma mudança de infraestrutura' })).toBeInTheDocument();
    expect(screen.getAllByText('Só analisa')).toHaveLength(6);
    expect(screen.getAllByText('Muda a nuvem')).toHaveLength(1);
    expect(screen.getByText('Prepara a mudança')).toBeInTheDocument();
  });
});
