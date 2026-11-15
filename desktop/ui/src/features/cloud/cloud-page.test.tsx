import catalogFixture from '@contracts/fixtures/cloud/catalog.json';
import connectionsFixture from '@contracts/fixtures/cloud/connections.json';
import linksFixture from '@contracts/fixtures/cloud/links.json';
import readFixture from '@contracts/fixtures/cloud/read.json';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router';
import { describe, expect, it } from 'vitest';

import type { EngineClient } from '@/shared/api';
import { AppProviders } from '@/shared/components';
import { expectNoA11yViolations } from '@/test/a11y';

import { CloudPage } from './cloud-page';

const demo = { projectId: 'p-0123456789ab', displayName: 'demo', displayPath: '~\\demo' };
const aws = connectionsFixture.connections[0]!;

function fakeEngine() {
  const calls: { method: string; params: Record<string, unknown> }[] = [];
  const client: EngineClient = {
    async request(method, params, schema) {
      calls.push({ method, params: params as Record<string, unknown> });
      const link = linksFixture.links[0]!;
      const reply: Record<string, unknown> = {
        'cloud.catalog': catalogFixture,
        'cloud.list': connectionsFixture,
        'cloud.create': { connection: { ...connectionsFixture.connections[1], name: 'Nova' } },
        'cloud.validate': { connection: aws },
        'cloud.permissions': { connection: aws },
        'cloud.links': linksFixture,
        'cloud.configure': { link: { ...link, ...(params as object) } },
        'cloud.link': { link },
        'cloud.read': readFixture
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
        <CloudPage client={client} project={project} />
      </MemoryRouter>
    </AppProviders>
  );
}

describe('Conexões cloud', () => {
  it('separa provedor disponível, estado da conexão e permissão de cada leitura', async () => {
    const { container } = renderPage(fakeEngine().client);

    expect(await screen.findByText('Amazon Web Services')).toBeInTheDocument();
    expect(screen.getByText('Microsoft Azure')).toBeInTheDocument();
    expect(screen.getByText('Google Cloud')).toBeInTheDocument();
    expect(screen.getByText('Conectada')).toBeInTheDocument();
    expect(screen.getByText('Aguardando autenticação')).toBeInTheDocument();
    expect(screen.getByText(/8 de 9 leituras permitidas/)).toBeInTheDocument();
    await expectNoA11yViolations(container);
  });

  it('pede só nomes ao conectar: o mecanismo troca os campos e não há campo de segredo', async () => {
    const engine = fakeEngine();
    renderPage(engine.client);

    await userEvent.click(await screen.findByRole('button', { name: 'Adicionar conexão Amazon Web Services' }));
    const form = screen.getByRole('form', { name: 'Nova conexão com Amazon Web Services' });
    expect(within(form).getByLabelText('Perfil')).toBeInTheDocument();
    expect(form.querySelector('input[type=password]')).toBeNull();

    await userEvent.click(within(form).getByRole('radio', { name: 'Assumir um papel (AssumeRole)' }));
    expect(within(form).getByLabelText('Role ARN')).toBeInTheDocument();
    await userEvent.type(within(form).getByLabelText('Role ARN'), 'arn:aws:iam::123456789012:role/leitura');
    await userEvent.type(within(form).getByLabelText('Região padrão'), 'sa-east-1');
    await userEvent.click(within(form).getByRole('button', { name: 'Conectar e validar' }));

    const create = engine.calls.find((call) => call.method === 'cloud.create');
    expect(create?.params).toEqual({
      provider: 'aws',
      name: 'Amazon Web Services',
      mechanism: 'assume-role',
      settings: { roleArn: 'arn:aws:iam::123456789012:role/leitura', region: 'sa-east-1' }
    });
  });

  it('lê um recurso permitido e mostra a tabela com as colunas da leitura', async () => {
    const engine = fakeEngine();
    renderPage(engine.client);

    await userEvent.click(await screen.findByRole('button', { name: 'Ver Regiões' }));

    const table = await screen.findByRole('table', { name: 'Regiões' });
    expect(within(table).getByRole('columnheader', { name: 'Habilitação' })).toBeInTheDocument();
    expect(engine.calls.find((call) => call.method === 'cloud.read')?.params).toMatchObject({
      connectionId: aws.id,
      capability: 'aws.regions'
    });
  });

  it('libera leituras e agentes no projeto só ao salvar', async () => {
    const engine = fakeEngine();
    renderPage(engine.client);

    const agents = await screen.findByRole('checkbox', { name: 'Agentes de IA podem pedir estas leituras pelo MCP' });
    await userEvent.click(agents);
    await userEvent.click(screen.getByRole('button', { name: 'Salvar' }));

    const configure = engine.calls.find((call) => call.method === 'cloud.configure');
    expect(configure?.params).toMatchObject({
      projectId: demo.projectId,
      agentAccess: !linksFixture.links[0]!.agentAccess
    });
  });
});
