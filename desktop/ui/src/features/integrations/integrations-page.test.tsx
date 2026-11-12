import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router';
import { describe, expect, it } from 'vitest';

import type { SelectedProject } from '@/features/project-selection';
import type { EngineClient } from '@/shared/api';
import { AppProviders } from '@/shared/components';
import { expectNoA11yViolations } from '@/test/a11y';
import { fakeIntegrationEngine, githubConnection } from '@/test/fake-integration-engine';

import { IntegrationsPage } from './integrations-page';

const loja: SelectedProject = { projectId: 'p-aaaaaaaaaaaa', displayName: 'Loja', displayPath: '~\\Loja' };
const portal: SelectedProject = { projectId: 'p-bbbbbbbbbbbb', displayName: 'Portal', displayPath: '~\\Portal' };
const token = 'ghp_token-digitado-na-tela-0001';

const page = (client: EngineClient, project: SelectedProject | null) => (
  <AppProviders>
    <MemoryRouter>
      <IntegrationsPage client={client} project={project} />
    </MemoryRouter>
  </AppProviders>
);

describe('Integrações · conexões', () => {
  it('conecta, mostra a conta que o provedor reconheceu e não deixa a credencial na tela', async () => {
    const engine = fakeIntegrationEngine();
    const { container } = render(page(engine.client, null));

    expect(await screen.findByText('Nenhuma conexão ainda.')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Conectar GitHub' }));

    const form = screen.getByRole('form', { name: 'Nova conexão com GitHub' });
    expect(within(form).getByRole('button', { name: 'Conectar' })).toBeDisabled();
    await userEvent.clear(within(form).getByLabelText('Nome da conexão'));
    await userEvent.type(within(form).getByLabelText('Nome da conexão'), 'GitHub da equipe');
    await userEvent.type(within(form).getByLabelText('Token de acesso pessoal'), token);
    await userEvent.click(within(form).getByRole('button', { name: 'Conectar' }));

    const row = (await screen.findByText('GitHub da equipe')).closest('li')!;
    expect(within(row).getByText(/octo/)).toBeInTheDocument();
    expect(within(row).getByText('Conectada')).toBeInTheDocument();
    expect(within(row).getByText(/Escopos informados: repo, read:org/)).toBeInTheDocument();
    expect(screen.queryByRole('form', { name: 'Nova conexão com GitHub' })).not.toBeInTheDocument();
    expect(container.innerHTML).not.toContain(token);
    expect(screen.getByText('GitHub da equipe conectada como octo.')).toBeInTheDocument();
    expect(engine.calls.find((call) => call.method === 'connection.create')?.params).toMatchObject({
      provider: 'github',
      secret: token
    });

    await expectNoA11yViolations(container);
  });

  it('diz o motivo quando o provedor recusa e mantém o formulário', async () => {
    const engine = fakeIntegrationEngine();
    render(page(engine.client, null));

    await userEvent.click(await screen.findByRole('button', { name: 'Conectar GitHub' }));
    await userEvent.type(screen.getByLabelText('Token de acesso pessoal'), 'token-recusado');
    await userEvent.click(screen.getByRole('button', { name: 'Conectar' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('O provedor recusou a credencial.');
    expect(screen.getByRole('form', { name: 'Nova conexão com GitHub' })).toBeInTheDocument();
  });

  it('sem armazenamento protegido não oferece conectar e explica por quê', async () => {
    const engine = fakeIntegrationEngine({ vaultAvailable: false });
    render(page(engine.client, null));

    expect(await screen.findByText('Armazenamento protegido indisponível')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Conectar GitHub' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Conectar Jira Cloud' })).toBeDisabled();
  });

  it('remover pede confirmação, diz o que acontece e só apaga depois de confirmada', async () => {
    const engine = fakeIntegrationEngine({ connections: [{ ...githubConnection, projects: 0 }] });
    const { container } = render(page(engine.client, null));
    const row = (await screen.findByText('GitHub da equipe')).closest('li')!;
    const removed = () => engine.calls.filter((call) => call.method === 'connection.remove');

    await userEvent.click(within(row).getByRole('button', { name: 'Remover' }));

    const confirm = within(row).getByRole('group', { name: 'Remover GitHub da equipe' });
    expect(confirm).toHaveTextContent(/a credencial é apagada deste computador/);
    expect(confirm).toHaveTextContent(/conectar outra vez, com uma credencial nova/);
    expect(removed()).toHaveLength(0);
    await expectNoA11yViolations(container);

    await userEvent.click(within(confirm).getByRole('button', { name: 'Manter' }));
    expect(within(row).queryByRole('group', { name: 'Remover GitHub da equipe' })).not.toBeInTheDocument();
    expect(removed()).toHaveLength(0);

    await userEvent.click(within(row).getByRole('button', { name: 'Remover' }));
    await userEvent.click(within(row).getByRole('button', { name: 'Remover conexão' }));

    await waitFor(() => expect(screen.queryByText('GitHub da equipe')).not.toBeInTheDocument());
    expect(removed().map((call) => call.params)).toEqual([{ connectionId: githubConnection.id }]);
    expect(
      screen.getByText('GitHub da equipe removida: a credencial foi apagada deste computador.')
    ).toBeInTheDocument();
  });

  it('desconectar pede confirmação e só apaga a credencial depois de confirmada', async () => {
    const engine = fakeIntegrationEngine({ connections: [githubConnection] });
    render(page(engine.client, null));
    const row = (await screen.findByText('GitHub da equipe')).closest('li')!;
    const disconnected = () => engine.calls.filter((call) => call.method === 'connection.disconnect');

    await userEvent.click(within(row).getByRole('button', { name: 'Desconectar' }));

    expect(within(row).getByRole('group', { name: 'Desconectar GitHub da equipe' })).toBeInTheDocument();
    expect(disconnected()).toHaveLength(0);

    await userEvent.click(within(row).getByRole('button', { name: 'Apagar credencial' }));

    expect(await within(row).findByText('Sem credencial')).toBeInTheDocument();
    expect(disconnected().map((call) => call.params)).toEqual([{ connectionId: githubConnection.id }]);
    expect(
      screen.getByText('GitHub da equipe desconectada: a credencial foi apagada deste computador.')
    ).toBeInTheDocument();
  });

  it('testar só confirma quando o provedor aceitou a credencial', async () => {
    const engine = fakeIntegrationEngine({ connections: [githubConnection] });
    render(page(engine.client, null));
    const row = (await screen.findByText('GitHub da equipe')).closest('li')!;

    await userEvent.click(within(row).getByRole('button', { name: 'Testar' }));

    expect(await screen.findByText('GitHub da equipe: o provedor aceitou a credencial.')).toBeInTheDocument();
  });

  it('testar não confirma nada quando o provedor recusa: a recusa aparece na conexão, não como sucesso', async () => {
    const engine = fakeIntegrationEngine({ connections: [githubConnection], testRefused: true });
    render(page(engine.client, null));
    const row = (await screen.findByText('GitHub da equipe')).closest('li')!;

    await userEvent.click(within(row).getByRole('button', { name: 'Testar' }));

    expect(await within(row).findByText(/O provedor recusou a credencial\./)).toBeInTheDocument();
    expect(engine.calls.some((call) => call.method === 'connection.test')).toBe(true);
    expect(screen.queryByText(/aceitou a credencial/)).not.toBeInTheDocument();
  });

  it('a mensagem de sucesso anterior some quando a próxima ação começa', async () => {
    const engine = fakeIntegrationEngine({ connections: [githubConnection] });
    render(page(engine.client, null));
    const row = (await screen.findByText('GitHub da equipe')).closest('li')!;
    await userEvent.click(within(row).getByRole('button', { name: 'Testar' }));
    expect(await screen.findByText(/o provedor aceitou a credencial/)).toBeInTheDocument();

    await userEvent.click(within(row).getByRole('button', { name: 'Desconectar' }));
    await userEvent.click(within(row).getByRole('button', { name: 'Apagar credencial' }));

    expect(await screen.findByText(/desconectada: a credencial foi apagada/)).toBeInTheDocument();
    expect(screen.queryByText(/o provedor aceitou a credencial/)).not.toBeInTheDocument();
  });
});

describe('Integrações · vínculo com o projeto', () => {
  it('sem projeto aberto, só as conexões são gerenciadas', async () => {
    const engine = fakeIntegrationEngine({ connections: [githubConnection] });
    render(page(engine.client, null));

    expect(await screen.findByText(/Nenhum projeto selecionado/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Vincular/ })).not.toBeInTheDocument();
    expect(engine.calls.some((call) => call.method === 'integration.list')).toBe(false);
  });

  it('vincula um recurso, sincroniza em segundo plano e mostra o que chegou', async () => {
    const engine = fakeIntegrationEngine({ connections: [githubConnection] });
    const { container } = render(page(engine.client, loja));

    expect(await screen.findByText('Nenhuma conexão vinculada a este projeto.')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Vincular GitHub da equipe' }));
    const form = screen.getByRole('form', { name: 'Vincular GitHub da equipe a este projeto' });
    await userEvent.type(within(form).getByLabelText('Repositório'), 'acme/app');
    await userEvent.click(within(form).getByLabelText('Pull requests'));
    await userEvent.click(within(form).getByRole('button', { name: 'Vincular' }));

    const row = (await screen.findByText('acme/app')).closest('li')!;
    expect(within(row).getByText(/Ainda não sincronizada/)).toBeInTheDocument();
    expect(screen.getByText('acme/app vinculado a este projeto. Sincronize para trazer os dados.')).toBeInTheDocument();
    expect(engine.calls.find((call) => call.method === 'integration.link')?.params).toEqual({
      projectId: loja.projectId,
      connectionId: githubConnection.id,
      resource: 'acme/app',
      dataKinds: ['issues']
    });

    await userEvent.click(within(row).getByRole('button', { name: 'Sincronizar' }));
    expect(await within(row).findByText('Sincronizando')).toBeInTheDocument();
    expect(within(row).getByRole('button', { name: 'Sincronizar' })).toBeDisabled();

    // A lista é relida sozinha até a sincronização terminar.
    await waitFor(() => expect(within(row).getByText('Concluída')).toBeInTheDocument(), { timeout: 5000 });
    expect(
      within(row).getByText(/Issues: 1 · Pull requests: 1 · lido .* · última sincronização: 2 novos/)
    ).toBeInTheDocument();

    await userEvent.click(within(row).getByRole('button', { name: 'Histórico' }));
    expect(await within(row).findByText(/completa · 2 novos/)).toBeInTheDocument();

    await expectNoA11yViolations(container);
  });

  it('o vínculo de um projeto não aparece quando outro projeto está aberto', async () => {
    const engine = fakeIntegrationEngine({ connections: [githubConnection] });
    engine.seed(loja.projectId);
    const { rerender } = render(page(engine.client, loja));
    expect(await screen.findByText('acme/app')).toBeInTheDocument();

    rerender(page(engine.client, portal));

    expect(screen.queryByText('acme/app')).not.toBeInTheDocument();
    expect(await screen.findByText('Nenhuma conexão vinculada a este projeto.')).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Neste projeto: Portal' })).toBeInTheDocument();
    expect(
      engine.calls.filter((call) => call.method === 'integration.list').map((call) => call.params.projectId)
    ).toEqual([loja.projectId, portal.projectId]);
  });

  it('desvincular diz o que será apagado e só age depois da confirmação', async () => {
    const engine = fakeIntegrationEngine({ connections: [githubConnection] });
    engine.seed(loja.projectId);
    render(page(engine.client, loja));
    const row = (await screen.findByText('acme/app')).closest('li')!;

    await userEvent.click(within(row).getByRole('button', { name: 'Desvincular' }));

    const confirm = within(row).getByRole('group', { name: 'Desvincular acme/app' });
    expect(confirm).toHaveTextContent('os 2 itens sincronizados por ele serão apagados deste computador');
    expect(engine.calls.some((call) => call.method === 'integration.unlink')).toBe(false);

    await userEvent.click(within(confirm).getByRole('button', { name: 'Desvincular e apagar' }));

    expect(await screen.findByText('Nenhuma conexão vinculada a este projeto.')).toBeInTheDocument();
  });
});
