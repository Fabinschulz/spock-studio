import reachFixture from '@contracts/fixtures/integrations/reach.json';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router';
import { describe, expect, it } from 'vitest';

import type { SelectedProject } from '@/features/project-selection';
import type { EngineClient } from '@/shared/api';
import { AppProviders } from '@/shared/components';
import { connectionReachListSchema, type ConnectionReach } from '@/shared/schema';
import { expectNoA11yViolations } from '@/test/a11y';
import { fakeIntegrationEngine, githubConnection } from '@/test/fake-integration-engine';

import { IntegrationsPage } from './integrations-page';

const loja: SelectedProject = { projectId: 'p-aaaaaaaaaaaa', displayName: 'Loja', displayPath: '~\\Loja' };
const portal: SelectedProject = { projectId: 'p-bbbbbbbbbbbb', displayName: 'Portal', displayPath: '~\\Portal' };
const token = 'ghp_token-digitado-na-tela-0001';
const reachFromEngine = connectionReachListSchema.parse(reachFixture).connections;

// O resumo de cada conexão também cita o repositório. Estas buscas ficam na seção do projeto, onde mora o vínculo.
const projectRegion = async () => within(await screen.findByRole('region', { name: /^Neste projeto/ }));
const linkedRow = async () => (await (await projectRegion()).findByText('acme/app')).closest('li')!;

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
    expect(within(row).getByText(/GitHub · octo/)).toBeInTheDocument();
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
    // A linha já não existe: a mensagem fica na seção, em vez de sumir com ela.
    expect(
      screen.getByText('GitHub da equipe removida: a credencial foi apagada deste computador.')
    ).toBeInTheDocument();
    expect(row).not.toBeInTheDocument();
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
      within(row).getByText('GitHub da equipe desconectada: a credencial foi apagada deste computador.')
    ).toBeInTheDocument();
  });

  it('a confirmação leva o foco à opção segura e o Esc desiste sem apagar nada', async () => {
    const engine = fakeIntegrationEngine({ connections: [{ ...githubConnection, projects: 0 }] });
    render(page(engine.client, null));
    const row = (await screen.findByText('GitHub da equipe')).closest('li')!;

    await userEvent.click(within(row).getByRole('button', { name: 'Remover' }));

    // O botão "Remover" saiu da tela: sem isto o foco cairia no vazio.
    expect(within(row).getByRole('button', { name: 'Manter' })).toHaveFocus();
    await userEvent.keyboard('{Escape}');

    expect(within(row).queryByRole('group', { name: 'Remover GitHub da equipe' })).not.toBeInTheDocument();
    expect(engine.calls.some((call) => call.method === 'connection.remove')).toBe(false);
  });

  it('a mensagem de uma ação de conexão fica na linha que agiu, não na de outra', async () => {
    const jira = { ...githubConnection, id: 'conn-jira', name: 'Jira da equipe', provider: 'jira' };
    const engine = fakeIntegrationEngine({ connections: [githubConnection, jira] });
    render(page(engine.client, null));
    const github = (await screen.findByText('GitHub da equipe')).closest('li')!;
    const other = screen.getByText('Jira da equipe').closest('li')!;

    await userEvent.click(within(github).getByRole('button', { name: 'Testar' }));

    expect(await within(github).findByText(/o provedor aceitou a credencial/)).toBeInTheDocument();
    expect(within(other).queryByText(/o provedor aceitou a credencial/)).not.toBeInTheDocument();
  });

  it('conectar não é de uma linha: a mensagem fica na seção', async () => {
    const engine = fakeIntegrationEngine();
    render(page(engine.client, null));

    await userEvent.click(await screen.findByRole('button', { name: 'Conectar GitHub' }));
    await userEvent.type(screen.getByLabelText('Token de acesso pessoal'), token);
    await userEvent.click(screen.getByRole('button', { name: 'Conectar' }));

    const notice = await screen.findByText('GitHub conectada como octo.');
    expect(notice.closest('li')).toBeNull();
  });

  it('testar só confirma quando o provedor aceitou a credencial', async () => {
    const engine = fakeIntegrationEngine({ connections: [githubConnection] });
    render(page(engine.client, null));
    const row = (await screen.findByText('GitHub da equipe')).closest('li')!;

    await userEvent.click(within(row).getByRole('button', { name: 'Testar' }));

    // A mensagem cai na linha da conexão que foi testada, não no fim da seção.
    expect(await within(row).findByText('GitHub da equipe: o provedor aceitou a credencial.')).toBeInTheDocument();
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

    const row = await linkedRow();
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

    // O motor avisa quando a sincronização termina e a lista é relida na hora, sem esperar a leitura de segurança.
    await waitFor(() => expect(within(row).getByText('Concluída')).toBeInTheDocument(), { timeout: 5000 });
    expect(engine.calls.filter((call) => call.method === 'integration.list').length).toBeLessThan(6);
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
    expect(await linkedRow()).toBeInTheDocument();

    rerender(page(engine.client, portal));

    expect((await projectRegion()).queryByText('acme/app')).not.toBeInTheDocument();
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
    const row = await linkedRow();

    await userEvent.click(within(row).getByRole('button', { name: 'Desvincular' }));

    const confirm = within(row).getByRole('group', { name: 'Desvincular acme/app' });
    expect(confirm).toHaveTextContent('os 2 itens sincronizados por ele serão apagados deste computador');
    expect(engine.calls.some((call) => call.method === 'integration.unlink')).toBe(false);

    await userEvent.click(within(confirm).getByRole('button', { name: 'Desvincular e apagar' }));

    expect(await screen.findByText('Nenhuma conexão vinculada a este projeto.')).toBeInTheDocument();
  });
});

describe('Integrações · avisos de sincronização', () => {
  const syncData = (projectId: string) => ({
    projectId,
    integrationId: 'int-000000000001',
    status: 'running',
    incremental: false,
    pages: 1,
    fetched: 2,
    created: 2,
    updated: 0,
    unchanged: 0,
    error: null
  });

  const openWithLink = async () => {
    const engine = fakeIntegrationEngine({ connections: [githubConnection] });
    engine.seed(loja.projectId);
    render(page(engine.client, loja));
    await linkedRow();
    const listCalls = () => engine.calls.filter((call) => call.method === 'integration.list').length;
    return { engine, listCalls };
  };

  it('um aviso de sincronização deste projeto faz a lista ser relida na hora', async () => {
    const { engine, listCalls } = await openWithLink();
    const before = listCalls();

    engine.emit('sync.progress', 'sync-000000000001', syncData(loja.projectId));

    await waitFor(() => expect(listCalls()).toBe(before + 1));
  });

  it('um aviso de outro projeto não relê nada', async () => {
    const { engine, listCalls } = await openWithLink();
    const before = listCalls();

    engine.emit('sync.progress', 'sync-000000000002', syncData(portal.projectId));
    await new Promise((resolve) => setTimeout(resolve, 50));

    expect(listCalls()).toBe(before);
  });

  it('um aviso fora do formato é ignorado, sem derrubar a tela', async () => {
    const { engine, listCalls } = await openWithLink();
    const before = listCalls();

    engine.emit('sync.progress', 'sync-000000000003', { projectId: loja.projectId });
    await new Promise((resolve) => setTimeout(resolve, 50));

    expect(listCalls()).toBe(before);
    expect(await linkedRow()).toBeInTheDocument();
  });

  it('avisos de outros assuntos não relêem a lista', async () => {
    const { engine, listCalls } = await openWithLink();
    const before = listCalls();

    engine.emit('run.progress', 'run-1', syncData(loja.projectId));
    await new Promise((resolve) => setTimeout(resolve, 50));

    expect(listCalls()).toBe(before);
  });

  it('para de ouvir quando a tela sai', async () => {
    const engine = fakeIntegrationEngine({ connections: [githubConnection] });
    engine.seed(loja.projectId);
    const { unmount } = render(page(engine.client, loja));
    await linkedRow();
    const before = engine.calls.filter((call) => call.method === 'integration.list').length;

    unmount();
    engine.emit('sync.progress', 'sync-000000000004', syncData(loja.projectId));
    await new Promise((resolve) => setTimeout(resolve, 50));

    expect(engine.calls.filter((call) => call.method === 'integration.list').length).toBe(before);
  });
});

describe('Integrações · o que a conexão alcança', () => {
  const rowOf = async () => (await screen.findByText('GitHub da equipe')).closest('li')!;
  const answerTo = (row: HTMLElement, question: string) => within(row).getByText(question).nextElementSibling!;
  const withProject = (change: Partial<ConnectionReach['projects'][number]>): ConnectionReach[] => [
    { ...reachFromEngine[0]!, projects: [{ ...reachFromEngine[0]!.projects[0]!, ...change }] }
  ];
  const ACCOUNT = 'Com qual conta o Spock Studio entra, e ela está funcionando?';
  const PROJECTS = 'Em quais projetos esta conexão é usada, e com qual recurso?';
  const READS = 'O que o Spock Studio lê de lá, e o que pode fazer?';
  const CONTEXT = 'Os agentes de IA podem usar esses dados como contexto?';

  it('responde as quatro perguntas com o que o motor disse', async () => {
    const engine = fakeIntegrationEngine({ connections: [githubConnection], reach: reachFromEngine });
    render(page(engine.client, null));

    const row = await rowOf();
    expect(answerTo(row, ACCOUNT)).toHaveTextContent('octo · Conectada');
    expect(answerTo(row, PROJECTS)).toHaveTextContent('loja: acme/app');
    expect(answerTo(row, READS)).toHaveTextContent('acme/app: Issues, Pull requests');
    expect(answerTo(row, READS)).toHaveTextContent(
      'Leitura: o Spock Studio só lê do GitHub. Não cria, altera nem apaga nada lá.'
    );
    expect(answerTo(row, CONTEXT)).toHaveTextContent(
      'acme/app: Não. Ao provedor de IA vão só números, datas e identificadores de itens, nunca os títulos.'
    );
    expect(engine.calls.some((call) => call.method === 'connection.reach')).toBe(true);
  });

  it('quando o projeto autorizou, diz o que passa a ser enviado ao provedor de IA', async () => {
    const engine = fakeIntegrationEngine({
      connections: [githubConnection],
      reach: withProject({ feedsKnowledge: true })
    });
    render(page(engine.client, null));

    const answer = answerTo(await rowOf(), CONTEXT);

    expect(answer).toHaveTextContent('acme/app: Sim.');
    expect(answer).toHaveTextContent(
      'Títulos de itens citados em riscos entram no que o assistente envia ao provedor de IA'
    );
    expect(answer).toHaveTextContent('só quando todas as fontes do projeto autorizam');
  });

  it('diz que o índice dos agentes de engenharia ainda não usa dado sincronizado e que a decisão é de cada projeto', async () => {
    const engine = fakeIntegrationEngine({ connections: [githubConnection], reach: reachFromEngine });
    render(page(engine.client, null));

    const answer = answerTo(await rowOf(), CONTEXT);

    expect(answer).toHaveTextContent(
      'O índice de conhecimento dos agentes de engenharia ainda não usa dado sincronizado'
    );
    expect(answer).toHaveTextContent('começa em "Não"');
  });

  it('conexão sem projeto diz que não há dado dela em projeto nenhum', async () => {
    const engine = fakeIntegrationEngine({ connections: [{ ...githubConnection, projects: 0 }] });
    render(page(engine.client, null));

    const row = await rowOf();

    expect(answerTo(row, PROJECTS)).toHaveTextContent('Em nenhum projeto ainda.');
    expect(answerTo(row, CONTEXT)).toHaveTextContent('Não há dado desta conexão em nenhum projeto.');
  });

  it('projeto que não está aberto neste computador aparece sem nome inventado', async () => {
    const engine = fakeIntegrationEngine({
      connections: [githubConnection],
      reach: withProject({ projectName: null })
    });
    render(page(engine.client, null));

    expect(answerTo(await rowOf(), PROJECTS)).toHaveTextContent(
      'Projeto que não está aberto neste computador: acme/app'
    );
  });

  it('impacto que a tela não conhece não vira "só lê"', async () => {
    const engine = fakeIntegrationEngine({
      connections: [githubConnection],
      reach: [{ ...reachFromEngine[0]!, impact: 'unknown' }]
    });
    render(page(engine.client, null));

    const text = answerTo(await rowOf(), READS);

    expect(text).toHaveTextContent('não reconhece o conector desta conexão');
    expect(text).not.toHaveTextContent('Leitura');
  });

  it('se só o resumo falha, a central continua inteira e a conexão diz que não conseguiu mostrá-lo', async () => {
    const engine = fakeIntegrationEngine({ connections: [githubConnection], reach: 'fails' });
    render(page(engine.client, null));

    const row = await rowOf();

    expect(within(row).getByText('O resumo do que esta conexão alcança não pôde ser lido agora.')).toBeInTheDocument();
    expect(within(row).getByRole('button', { name: 'Testar' })).toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('vincular um recurso aparece no resumo da conexão depois de reler', async () => {
    const engine = fakeIntegrationEngine({ connections: [githubConnection] });
    render(page(engine.client, loja));

    await userEvent.click(await screen.findByRole('button', { name: 'Vincular GitHub da equipe' }));
    const form = screen.getByRole('form', { name: 'Vincular GitHub da equipe a este projeto' });
    await userEvent.type(within(form).getByLabelText('Repositório'), 'acme/app');
    await userEvent.click(within(form).getByRole('button', { name: 'Vincular' }));

    await waitFor(() =>
      expect(answerTo(screen.getByText('GitHub da equipe').closest('li')!, PROJECTS)).toHaveTextContent(
        'Loja: acme/app'
      )
    );
  });

  it('não tem violações de acessibilidade', async () => {
    const engine = fakeIntegrationEngine({ connections: [githubConnection], reach: reachFromEngine });
    const { container } = render(page(engine.client, null));
    await rowOf();

    await expectNoA11yViolations(container);
  });
});
