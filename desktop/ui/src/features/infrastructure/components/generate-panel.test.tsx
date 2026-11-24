import optionsFixture from '@contracts/fixtures/iac/generate-options.json';
import generateFixture from '@contracts/fixtures/iac/generate-plan.json';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router';
import { describe, expect, it } from 'vitest';

import type { EngineClient } from '@/shared/api';
import { AppProviders } from '@/shared/components';

import { createIacApi } from '../service/iac-api';
import { GeneratePanel } from './generate-panel';

const project = { projectId: 'p-0123456789ab', displayName: 'loja-api', displayPath: '~\\loja-api' };

// Caracteriza o que cada nuvem mostra no desenho e o que vai ao motor; o painel é refeito por partes (UI-19, UI-20).
function renderPanel(replies: Record<string, unknown> = {}) {
  const calls: { method: string; params: Record<string, unknown> }[] = [];
  const client: EngineClient = {
    async request(method, params, schema) {
      calls.push({ method, params: params as Record<string, unknown> });
      const reply: Record<string, unknown> = {
        'iac.architect.proposal': { proposal: null },
        'iac.generate.options': optionsFixture,
        'iac.generate.plan': generateFixture,
        ...replies
      };
      return schema.parse(reply[method]);
    },
    async onEvent() {
      return () => undefined;
    }
  };
  render(
    <AppProviders>
      <MemoryRouter>
        <GeneratePanel client={client} api={createIacApi(client)} project={project} linked={[]} />
      </MemoryRouter>
    </AppProviders>
  );
  const plan = () => calls.find((call) => call.method === 'iac.generate.plan')?.params;
  return { calls, plan };
}

const panel = () => screen.findByRole('group', { name: 'Desenho da infraestrutura' });

const radioNames = (group: HTMLElement) =>
  within(group)
    .getAllByRole('radio')
    .map((radio) => radio.getAttribute('value'));

describe('Desenho da infraestrutura por nuvem', () => {
  it('na AWS oferece os tamanhos do Fargate, certificado, IP público e os limites do RDS', async () => {
    renderPanel();
    const view = await panel();

    expect(radioNames(within(view).getByRole('group', { name: 'CPU e memória' }))).toEqual([
      '256/512',
      '512/1024',
      '1024/2048',
      '2048/4096'
    ]);
    expect(within(view).getByRole('radio', { name: '256 CPU, 512 MB' })).toBeChecked();
    expect(within(view).getByLabelText('Certificado ACM (ARN)')).toBeInTheDocument();
    expect(within(view).getByLabelText(/Tarefas com IP público/)).toBeInTheDocument();
    expect(within(view).getByLabelText('Tarefas rodando')).toHaveValue('1');
    expect(within(view).getByLabelText('Filas SQS')).toBeInTheDocument();
    expect(within(view).getByLabelText('Região')).toHaveAccessibleDescription('Ex.: sa-east-1.');

    await userEvent.click(within(view).getByLabelText(/Banco RDS/));
    expect(radioNames(within(view).getByRole('group', { name: 'Motor' }))).toEqual(['postgres', 'mysql']);
    expect(within(view).getByLabelText('Classe da instância')).toHaveValue('db.t4g.micro');
    expect(within(view).getByLabelText('Armazenamento (GB)')).toHaveValue('20');
    expect(within(view).getByLabelText('Armazenamento (GB)')).toHaveAccessibleDescription('Entre 20 e 1000.');
    expect(within(view).getByLabelText('Multi-AZ (réplica em outra zona)')).toBeInTheDocument();
  });

  it('no Google mostra o Cloud Run, o projeto e a rede do banco, e manda o desenho com os padrões dele', async () => {
    const { plan } = renderPanel();
    const view = await panel();
    await userEvent.click(within(view).getByRole('radio', { name: 'Google Cloud' }));

    expect(within(view).getByText(/proposta com a IA do Spock é só para AWS/)).toBeInTheDocument();
    expect(within(view).getByLabelText(/Cloud Run com HTTPS gerenciado/)).toBeChecked();
    expect(radioNames(within(view).getByRole('group', { name: 'CPU e memória' }))).toEqual([
      '1024/512',
      '1024/2048',
      '2048/4096',
      '4096/8192'
    ]);
    expect(within(view).getByRole('radio', { name: '1024 CPU, 512 MB' })).toBeChecked();
    expect(within(view).queryByLabelText('Certificado ACM (ARN)')).not.toBeInTheDocument();
    expect(within(view).queryByLabelText(/IP público/)).not.toBeInTheDocument();
    expect(within(view).getByLabelText('Réplicas mínimas')).toBeInTheDocument();
    expect(within(view).getByLabelText('Fluxos do Pub/Sub')).toBeInTheDocument();
    expect(within(view).queryByLabelText('Rede VPC do banco')).not.toBeInTheDocument();

    await userEvent.click(within(view).getByLabelText(/Banco Cloud SQL/));
    expect(within(view).getByLabelText('Tier')).toHaveValue('db-f1-micro');
    expect(within(view).getByLabelText('Armazenamento (GB)')).toHaveValue('10');
    expect(within(view).getByLabelText('Armazenamento (GB)')).toHaveAccessibleDescription('Entre 10 e 1000.');
    expect(within(view).getByLabelText('Alta disponibilidade (réplica em outra zona)')).toBeInTheDocument();

    await userEvent.type(within(view).getByLabelText('Nome do projeto'), 'loja-api');
    await userEvent.type(within(view).getByLabelText('Região'), 'southamerica-east1');
    await userEvent.type(within(view).getByLabelText('Projeto do Google Cloud'), 'loja-producao-123');
    await userEvent.type(
      within(view).getByLabelText('Rede VPC do banco'),
      'projects/loja-producao-123/global/networks/loja'
    );
    await userEvent.click(within(view).getByRole('button', { name: 'Revisar os arquivos' }));

    await screen.findByLabelText('Revisar a infraestrutura gerada');
    expect(plan()).toMatchObject({
      blueprint: {
        provider: 'gcp',
        account: 'loja-producao-123',
        network: { vpcId: 'projects/loja-producao-123/global/networks/loja' },
        service: { cpu: 1024, memory: 512, certificateArn: '', publicIp: false },
        database: { engine: 'postgres', instanceClass: 'db-f1-micro', storageGb: 10 }
      }
    });
  }, 20_000);

  it('no Azure o banco é só PostgreSQL, pede subnet delegada e zona DNS, e o state leva grupo e container', async () => {
    const { plan } = renderPanel();
    const view = await panel();
    await userEvent.click(within(view).getByRole('radio', { name: 'Azure' }));
    await userEvent.click(within(view).getByLabelText(/PostgreSQL Flexible Server/));

    expect(radioNames(within(view).getByRole('group', { name: 'Motor' }))).toEqual(['postgres']);
    expect(within(view).getByLabelText('SKU')).toHaveValue('B_Standard_B1ms');
    expect(within(view).getByLabelText('Armazenamento (GB)')).toHaveAccessibleDescription(
      '32, 64, 128, 256, 512 ou 1024.'
    );
    expect(within(view).getByLabelText('Filas do Service Bus')).toBeInTheDocument();
    expect(within(view).queryByLabelText('Grupo de recursos da storage account')).not.toBeInTheDocument();

    await userEvent.type(within(view).getByLabelText('Subnet delegada ao PostgreSQL'), 'subnet-id');
    await userEvent.type(within(view).getByLabelText('Zona DNS privada do PostgreSQL'), 'zona-id');
    await userEvent.type(within(view).getByLabelText('Storage account de state (opcional)'), 'lojatfstate');
    await userEvent.type(within(view).getByLabelText('Grupo de recursos da storage account'), 'loja-tfstate');
    await userEvent.type(within(view).getByLabelText('Container do state'), 'tfstate');
    await userEvent.click(within(view).getByRole('button', { name: 'Revisar os arquivos' }));

    await screen.findByLabelText('Revisar a infraestrutura gerada');
    expect(plan()).toMatchObject({
      blueprint: {
        provider: 'azure',
        network: { privateSubnets: ['subnet-id'], privateDnsZone: 'zona-id' },
        database: { engine: 'postgres', instanceClass: 'B_Standard_B1ms', storageGb: 32 },
        backend: { bucket: 'lojatfstate', resourceGroup: 'loja-tfstate', container: 'tfstate' }
      }
    });
  }, 20_000);

  it('desenho que não passou nas verificações não oferece gravar, e voltar mantém o que foi digitado', async () => {
    const failing = {
      plan: {
        ...generateFixture.plan,
        changes: [],
        checks: [
          { id: 'vpc', description: 'VPC existente no formato vpc-...', passed: false, detail: 'Ex.: vpc-0a1b.' }
        ]
      }
    };
    renderPanel({ 'iac.generate.plan': failing });
    const view = await panel();
    await userEvent.type(within(view).getByLabelText('Nome do projeto'), 'loja-api');
    await userEvent.click(within(view).getByRole('button', { name: 'Revisar os arquivos' }));

    const review = await screen.findByLabelText('Revisar a infraestrutura gerada');
    expect(within(review).getByText('O desenho não passou nas verificações')).toBeInTheDocument();
    expect(within(review).getByText('VPC existente no formato vpc-... (Ex.: vpc-0a1b.)')).toBeInTheDocument();
    expect(within(review).queryByRole('button', { name: 'Gravar no projeto' })).not.toBeInTheDocument();

    await userEvent.click(within(review).getByRole('button', { name: 'Voltar ao desenho' }));
    expect(within(await panel()).getByLabelText('Nome do projeto')).toHaveValue('loja-api');
  });
});
