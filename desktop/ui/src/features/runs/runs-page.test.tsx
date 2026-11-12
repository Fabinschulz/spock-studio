import runDetailFixture from '@contracts/fixtures/runs/run-detail.json';
import runLinkFixture from '@contracts/fixtures/runs/run-link.json';
import runListFixture from '@contracts/fixtures/runs/run-list.json';

import { EngineError, type EngineClient } from '@/shared/api';
import { AppProviders } from '@/shared/components';
import type { RunItem } from '@/shared/schema';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router';
import { describe, expect, it, vi } from 'vitest';

import { RunsPage } from './runs-page';

const project = { projectId: 'p-000000000001', displayName: 'Loja', displayPath: '~\\Loja' };

const item = (overrides: Partial<RunItem>): RunItem => ({ ...(runListFixture.runs[0] as RunItem), ...overrides });

type Call = { method: string; params: Record<string, unknown> };

/** Um motor que responde só `run.list` e `run.detail`, e guarda tudo o que foi pedido. */
function engine(
  handlers: {
    list?: (params: Record<string, unknown>) => unknown;
    detail?: () => unknown;
    link?: (params: Record<string, unknown>) => unknown;
    unlink?: (params: Record<string, unknown>) => unknown;
  } = {}
) {
  const calls: Call[] = [];
  const client = {
    request: vi.fn(
      async (method: string, params: Record<string, unknown>, schema: { parse(value: unknown): unknown }) => {
        calls.push({ method, params });
        if (method === 'run.list') return schema.parse(handlers.list ? handlers.list(params) : runListFixture);
        if (method === 'run.detail') return schema.parse(handlers.detail ? handlers.detail() : runDetailFixture);
        if (method === 'run.link') return schema.parse(handlers.link ? handlers.link(params) : runLinkFixture);
        if (method === 'run.unlink') return schema.parse(handlers.unlink ? handlers.unlink(params) : { links: [] });
        throw new Error(`Método inesperado: ${method}`);
      }
    ),
    onEvent: vi.fn()
  } as unknown as EngineClient;
  return { client, calls };
}

const renderPage = (client: EngineClient, selected: typeof project | null = project) =>
  render(
    <AppProviders>
      <MemoryRouter>
        <RunsPage client={client} project={selected} />
      </MemoryRouter>
    </AppProviders>
  );

const listCalls = (calls: Call[]) => calls.filter((call) => call.method === 'run.list');

describe('Execuções', () => {
  it('lista as execuções, da mais recente para a mais antiga, com o projeto e o estado', async () => {
    const { client } = engine();
    renderPage(client);

    const list = await screen.findByRole('list', { name: 'Execuções' });
    const rows = within(list).getAllByRole('button');

    expect(rows).toHaveLength(3);
    expect(rows[0]).toHaveTextContent('Workflow');
    expect(rows[0]).toHaveTextContent('Aguardando aprovação');
    expect(rows[1]).toHaveTextContent('Falhou');
    expect(rows[2]).toHaveTextContent('Configuração aplicada');
    expect(rows[2]).toHaveTextContent('alterações aplicadas');
    expect(rows[0]).toHaveTextContent('app');
    expect(screen.getByText('1 aguardando aprovação')).toBeTruthy();
  });

  it('pede a primeira página sem projeto e sem filtros', async () => {
    const { client, calls } = engine();
    renderPage(client);
    await screen.findByRole('list', { name: 'Execuções' });

    expect(listCalls(calls)[0]?.params).toEqual({ limit: 25 });
  });

  it('filtra por tipo e por estado pedindo ao motor de novo', async () => {
    const { client, calls } = engine();
    renderPage(client);
    await screen.findByRole('list', { name: 'Execuções' });

    await userEvent.selectOptions(screen.getByLabelText('Tipo'), 'workflow');
    await waitFor(() => expect(listCalls(calls).at(-1)?.params).toMatchObject({ kinds: ['workflow'] }));

    await userEvent.selectOptions(screen.getByLabelText('Estado'), 'failed');
    await waitFor(() =>
      expect(listCalls(calls).at(-1)?.params).toMatchObject({ kinds: ['workflow'], states: ['failed'] })
    );
  });

  it('diz que nada combina quando o filtro não traz execuções', async () => {
    const { client } = engine({
      list: (params) =>
        params.kinds ? { runs: [], nextCursor: null, summary: runListFixture.summary } : runListFixture
    });
    renderPage(client);
    await screen.findByRole('list', { name: 'Execuções' });

    await userEvent.selectOptions(screen.getByLabelText('Tipo'), 'validate');

    expect(await screen.findByText('Nenhuma execução combina com esses filtros.')).toBeTruthy();
  });

  it('só oferece o escopo do projeto quando há um projeto aberto, e o usa no pedido', async () => {
    const withProject = engine();
    const { unmount } = renderPage(withProject.client);
    await screen.findByRole('list', { name: 'Execuções' });

    await userEvent.selectOptions(screen.getByLabelText('Projeto'), 'one');
    await waitFor(() =>
      expect(listCalls(withProject.calls).at(-1)?.params).toMatchObject({ projectId: project.projectId })
    );
    expect(screen.getByText(/tudo o que o Spock Studio executou neste projeto/)).toBeTruthy();
    unmount();

    renderPage(engine().client, null);
    await screen.findByRole('list', { name: 'Execuções' });
    expect(screen.queryByLabelText('Projeto')).toBeNull();
  });

  it('carrega mais com o cursor e esconde o botão quando acabou', async () => {
    const first = { ...runListFixture, runs: runListFixture.runs.slice(0, 2), nextCursor: 'cursor-1' };
    const second = { ...runListFixture, runs: runListFixture.runs.slice(2), nextCursor: null };
    const { client, calls } = engine({ list: (params) => (params.cursor === 'cursor-1' ? second : first) });
    renderPage(client);

    await userEvent.click(await screen.findByRole('button', { name: 'Carregar mais' }));

    await waitFor(() =>
      expect(within(screen.getByRole('list', { name: 'Execuções' })).getAllByRole('button')).toHaveLength(3)
    );
    expect(listCalls(calls).at(-1)?.params).toMatchObject({ cursor: 'cursor-1' });
    expect(screen.queryByRole('button', { name: 'Carregar mais' })).toBeNull();
  });

  it('um filtro novo descarta as páginas carregadas e começa de novo', async () => {
    const first = { ...runListFixture, runs: runListFixture.runs.slice(0, 2), nextCursor: 'cursor-1' };
    const { client, calls } = engine({ list: () => first });
    renderPage(client);
    await userEvent.click(await screen.findByRole('button', { name: 'Carregar mais' }));

    await userEvent.selectOptions(screen.getByLabelText('Tipo'), 'apply');

    await waitFor(() => expect(listCalls(calls).at(-1)?.params).not.toHaveProperty('cursor'));
    expect(listCalls(calls).at(-1)?.params).toMatchObject({ kinds: ['apply'] });
  });

  it('mostra a falha explicada e tenta de novo', async () => {
    let failing = true;
    const { client } = engine({
      list: () => {
        if (failing) throw new Error('motor fora');
        return runListFixture;
      }
    });
    renderPage(client);

    expect(await screen.findByRole('alert')).toBeTruthy();
    failing = false;
    await userEvent.click(screen.getByRole('button', { name: /Tentar de novo/ }));

    expect(await screen.findByRole('list', { name: 'Execuções' })).toBeTruthy();
  });

  it('uma execução andando mas sem ninguém executando aparece como interrompida', async () => {
    const { client } = engine({
      list: () => ({
        ...runListFixture,
        runs: [
          item({ runId: 'run-9', state: 'running', live: false }),
          item({ runId: 'run-8', state: 'running', live: true })
        ]
      })
    });
    renderPage(client);

    const rows = within(await screen.findByRole('list', { name: 'Execuções' })).getAllByRole('button');

    expect(rows[0]).toHaveTextContent('Interrompida');
    expect(rows[1]).toHaveTextContent('Em execução');
  });
});

describe('Execuções · detalhe', () => {
  const openFirstApplied = async () => {
    const harness = engine();
    renderPage(harness.client);
    const rows = within(await screen.findByRole('list', { name: 'Execuções' })).getAllByRole('button');
    const row = rows[2]!;
    await userEvent.click(row);
    return { ...harness, row };
  };

  it('abre o detalhe num painel com o que mudou, os passos e as verificações', async () => {
    const { calls } = await openFirstApplied();

    const panel = await screen.findByRole('dialog');
    expect(await within(panel).findByText('CLAUDE.md')).toBeTruthy();
    expect(within(panel).getByText('Aplicar')).toBeTruthy();
    expect(within(panel).getByText(/obrigatória/)).toBeTruthy();
    expect(within(panel).getByRole('heading', { name: 'Resumo da entrega' })).toBeTruthy();
    expect(within(panel).getByText('Criou 1 arquivo. 1 passo concluído de 1.')).toBeTruthy();
    expect(within(panel).getByText('1 de 1 verificação passou.')).toBeTruthy();
    expect(within(panel).getByText('Nada ficou de fora.')).toBeTruthy();
    expect(calls.find((call) => call.method === 'run.detail')?.params).toEqual({
      projectId: 'p-000000000001',
      runId: 'run-1'
    });
  });

  it('abrir o detalhe só lê: nenhum método que dispare, reverta ou valide é chamado', async () => {
    const { calls } = await openFirstApplied();
    await screen.findByRole('dialog');

    expect(new Set(calls.map((call) => call.method))).toEqual(new Set(['run.list', 'run.detail']));
  });

  it('o Esc fecha o painel e o foco volta para a linha que o abriu', async () => {
    const { row } = await openFirstApplied();
    await screen.findByRole('dialog');

    await userEvent.keyboard('{Escape}');

    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
    await waitFor(() => expect(row).toHaveFocus());
  });

  it('o botão fechar também fecha o painel', async () => {
    await openFirstApplied();
    await screen.findByRole('dialog');

    await userEvent.click(screen.getByRole('button', { name: 'Fechar o detalhe' }));

    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
  });

  it('uma falha mostra o motivo e o próximo passo', async () => {
    const harness = engine();
    renderPage(harness.client);
    const rows = within(await screen.findByRole('list', { name: 'Execuções' })).getAllByRole('button');

    await userEvent.click(rows[1]!);

    const panel = await screen.findByRole('dialog');
    expect(within(panel).getByText('O runtime de IA não respondeu.')).toBeTruthy();
    expect(within(panel).getByText(/nada foi aplicado/)).toBeTruthy();
  });

  it('só oferece o acompanhamento de quem anda agora ou espera aprovação', async () => {
    const harness = engine();
    renderPage(harness.client);
    const rows = within(await screen.findByRole('list', { name: 'Execuções' })).getAllByRole('button');

    await userEvent.click(rows[0]!);
    const waiting = await screen.findByRole('dialog');
    expect(within(waiting).getByRole('link', { name: 'Abrir o acompanhamento' })).toHaveAttribute(
      'href',
      '/execucao/run-3'
    );
    await userEvent.keyboard('{Escape}');
    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());

    await userEvent.click(rows[2]!);
    const finished = await screen.findByRole('dialog');
    expect(within(finished).queryByRole('link', { name: 'Abrir o acompanhamento' })).toBeNull();
  });

  it('um detalhe que não carrega diz o que houve sem fechar o painel', async () => {
    const harness = engine({
      detail: () => {
        throw new Error('diário ilegível');
      }
    });
    renderPage(harness.client);
    const rows = within(await screen.findByRole('list', { name: 'Execuções' })).getAllByRole('button');

    await userEvent.click(rows[2]!);

    const panel = await screen.findByRole('dialog');
    expect(await within(panel).findByRole('alert')).toBeTruthy();
  });
});

describe('Execuções · itens de trabalho', () => {
  const openDetail = async (harness = engine()) => {
    renderPage(harness.client);
    const rows = within(await screen.findByRole('list', { name: 'Execuções' })).getAllByRole('button');
    await userEvent.click(rows[2]!);
    const panel = await screen.findByRole('dialog');
    await within(panel).findByText('CLAUDE.md');
    return { ...harness, panel };
  };

  it('sem ligação diz isso, e o botão Ligar só vale com algo digitado', async () => {
    const { panel } = await openDetail();

    expect(within(panel).getByText('Nenhum item de trabalho ligado a esta execução.')).toBeTruthy();
    expect(within(panel).getByRole('button', { name: 'Ligar' })).toBeDisabled();
  });

  it('liga pelo número, mostra o item com título e situação e limpa o campo', async () => {
    const { panel, calls } = await openDetail();

    await userEvent.type(within(panel).getByLabelText('Ligar a um item (número ou chave)'), '#41');
    await userEvent.click(within(panel).getByRole('button', { name: 'Ligar' }));

    const list = await within(panel).findByRole('list', { name: 'Itens ligados' });
    expect(list).toHaveTextContent('#41');
    expect(list).toHaveTextContent('Pagamento por Pix');
    expect(list).toHaveTextContent('GitHub');
    expect(list).toHaveTextContent('open');
    expect(calls.find((call) => call.method === 'run.link')?.params).toEqual({
      projectId: 'p-000000000001',
      runId: 'run-1',
      externalId: '#41'
    });
    expect(within(panel).getByLabelText('Ligar a um item (número ou chave)')).toHaveValue('');
  });

  it('o motor recusa um item que não foi sincronizado e a tela diz o motivo sem perder o que digitou', async () => {
    const { panel } = await openDetail(
      engine({
        link: () => {
          throw new EngineError(
            'LINK_ITEM_NOT_FOUND',
            'Nenhum item sincronizado deste projeto tem esse identificador.'
          );
        }
      })
    );

    await userEvent.type(within(panel).getByLabelText('Ligar a um item (número ou chave)'), '999');
    await userEvent.click(within(panel).getByRole('button', { name: 'Ligar' }));

    expect(await within(panel).findByRole('alert')).toHaveTextContent('Nenhum item sincronizado deste projeto');
    expect(within(panel).getByLabelText('Ligar a um item (número ou chave)')).toHaveValue('999');
  });

  it('desliga pelo botão do item, com o pedido certo', async () => {
    const detailWithLink = { ...runDetailFixture, links: runLinkFixture.links };
    const { panel, calls } = await openDetail(engine({ detail: () => detailWithLink }));

    await userEvent.click(within(panel).getByRole('button', { name: 'Desligar o item 41' }));

    await waitFor(() => expect(within(panel).queryByRole('list', { name: 'Itens ligados' })).toBeNull());
    expect(calls.find((call) => call.method === 'run.unlink')?.params).toEqual({
      projectId: 'p-000000000001',
      runId: 'run-1',
      integrationId: 'int-000000000001',
      kind: 'issues',
      externalId: '41'
    });
  });

  it('um item que saiu do recorte aparece como tal, sem título nem situação', async () => {
    const gone = { ...runLinkFixture.links[0]!, available: false, title: null, status: null, statusCategory: null };
    const { panel } = await openDetail(engine({ detail: () => ({ ...runDetailFixture, links: [gone] }) }));

    const list = within(panel).getByRole('list', { name: 'Itens ligados' });
    expect(list).toHaveTextContent('fora do recorte atual da integração');
    expect(list).not.toHaveTextContent('Pagamento por Pix');
  });
});
