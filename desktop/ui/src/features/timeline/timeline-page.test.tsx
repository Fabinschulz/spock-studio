import timelineFixture from '@contracts/fixtures/timeline/timeline.json';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router';
import { describe, expect, it, vi } from 'vitest';

import type { SelectedProject } from '@/features/project-selection';
import { EngineError, type EngineClient } from '@/shared/api';
import { projectTimelineSchema, type ProjectTimeline } from '@/shared/schema';
import { expectNoA11yViolations } from '@/test/a11y';

import { TimelinePage } from './timeline-page';

const project: SelectedProject = { projectId: 'p-000000000001', displayName: 'Loja', displayPath: '~\\Loja' };
const full = projectTimelineSchema.parse(timelineFixture);

type Responder = (params: Record<string, unknown>) => ProjectTimeline | Error;

function clientWith(responder: Responder) {
  const request = vi.fn(
    async (_method: string, params: Record<string, unknown>, schema: { parse(value: unknown): unknown }) => {
      const answer = responder(params);
      if (answer instanceof Error) throw answer;
      return schema.parse(answer);
    }
  );
  return { client: { request, onEvent: vi.fn() } as unknown as EngineClient, request };
}

const renderPage = (client: EngineClient, selected: SelectedProject | null = project) =>
  render(
    <MemoryRouter>
      <TimelinePage client={client} project={selected} />
    </MemoryRouter>
  );

/** Uma linha do tempo em páginas de `size`: a próxima pede o corte que a anterior devolveu. */
function paged(size: number): Responder {
  return (params) => {
    const before = params.before as string | undefined;
    const rest = before ? full.events.filter((event) => event.at < before) : full.events;
    const events = rest.slice(0, size);
    return { ...full, events, nextBefore: rest.length > size ? events[events.length - 1]!.at : null };
  };
}

describe('Linha do tempo', () => {
  it('pede a linha do tempo do projeto aberto e só lê', async () => {
    const { client, request } = clientWith(() => full);
    renderPage(client);

    await screen.findByRole('list', { name: 'Acontecimentos do projeto' });

    expect(request).toHaveBeenCalledTimes(1);
    expect(request).toHaveBeenCalledWith(
      'timeline.read',
      { projectId: project.projectId, limit: 30 },
      expect.anything()
    );
  });

  it('mostra cada acontecimento do mais recente ao mais antigo, com o tipo, o nível dito em palavra e o detalhe', async () => {
    renderPage(clientWith(() => full).client);

    const list = await screen.findByRole('list', { name: 'Acontecimentos do projeto' });
    const items = within(list).getAllByRole('listitem');

    expect(items).toHaveLength(full.events.length);
    expect(items[0]).toHaveTextContent('Decisão: Adiar o marco em duas semanas');
    expect(items[0]).toHaveTextContent('Decisão');
    expect(items[0]).toHaveTextContent('Registrada com 1 fato do momento.');
    const raised = items.find((item) => item.textContent?.includes('subiu de gravidade'))!;
    expect(raised).toHaveTextContent('Atenção');
    const failed = items.find((item) => item.textContent?.includes('Pedido de engenharia'))!;
    expect(failed).toHaveTextContent('Problema');
    expect(failed).toHaveTextContent('Falhou.');
    const info = items.find((item) => item.textContent?.includes('Next.js mudou'))!;
    expect(info).not.toHaveTextContent('Atenção');
    expect(info).not.toHaveTextContent('Problema');
  });

  it('uma execução leva ao detalhe dela e uma decisão leva ao acompanhamento', async () => {
    renderPage(clientWith(() => full).client);

    const list = await screen.findByRole('list', { name: 'Acontecimentos do projeto' });
    const run = within(list).getAllByRole('link', { name: 'Ver execução' })[0]!;

    expect(run.getAttribute('href')).toMatch(/^\/execucao\/ex-/);
    expect(within(list).getByRole('link', { name: 'Ver em Acompanhamento' })).toHaveAttribute('href', '/gestao');
  });

  it('os filtros trazem a contagem de cada tipo e pedem só os tipos escolhidos', async () => {
    const { client, request } = clientWith((params) => {
      const kinds = (params.kinds as string[] | undefined) ?? [];
      return kinds.length === 0
        ? full
        : { ...full, events: full.events.filter((event) => kinds.includes(event.kind)), nextBefore: null };
    });
    renderPage(client);

    const group = await screen.findByRole('group', { name: 'Tipos de acontecimento' });
    expect(within(group).getByRole('button', { name: 'Riscos (3)' })).toHaveAttribute('aria-pressed', 'false');

    await userEvent.click(within(group).getByRole('button', { name: 'Riscos (3)' }));

    await waitFor(() =>
      expect(request).toHaveBeenLastCalledWith(
        'timeline.read',
        { projectId: project.projectId, kinds: ['risk'], limit: 30 },
        expect.anything()
      )
    );
    const list = await screen.findByRole('list', { name: 'Acontecimentos do projeto' });
    expect(within(list).getAllByRole('listitem')).toHaveLength(3);
    expect(
      within(screen.getByRole('group', { name: 'Tipos de acontecimento' })).getByRole('button', { name: 'Riscos (3)' })
    ).toHaveAttribute('aria-pressed', 'true');

    await userEvent.click(
      within(screen.getByRole('group', { name: 'Tipos de acontecimento' })).getByRole('button', { name: 'Riscos (3)' })
    );
    await waitFor(() =>
      expect(request).toHaveBeenLastCalledWith(
        'timeline.read',
        { projectId: project.projectId, limit: 30 },
        expect.anything()
      )
    );
  });

  it('"Ver mais antigos" pede a página seguinte pelo corte e junta sem repetir, e some no fim', async () => {
    const { client, request } = clientWith(paged(4));
    renderPage(client);

    const list = await screen.findByRole('list', { name: 'Acontecimentos do projeto' });
    expect(within(list).getAllByRole('listitem')).toHaveLength(4);

    await userEvent.click(screen.getByRole('button', { name: 'Ver mais antigos' }));
    await waitFor(() => expect(within(list).getAllByRole('listitem')).toHaveLength(8));
    expect(request).toHaveBeenLastCalledWith(
      'timeline.read',
      { projectId: project.projectId, limit: 30, before: full.events[3]!.at },
      expect.anything()
    );

    await userEvent.click(screen.getByRole('button', { name: 'Ver mais antigos' }));
    await waitFor(() => expect(within(list).getAllByRole('listitem')).toHaveLength(full.events.length));
    expect(screen.queryByRole('button', { name: 'Ver mais antigos' })).toBeNull();
    const ids = within(list)
      .getAllByRole('listitem')
      .map((item) => item.textContent);
    expect(new Set(ids).size).toBe(ids.length);
  });

  it('se carregar mais falha, a lista continua e o motivo aparece', async () => {
    let calls = 0;
    const { client } = clientWith((params) => {
      calls += 1;
      if (calls > 1) return new EngineError('TIMELINE_UNAVAILABLE', 'A leitura falhou.');
      return paged(4)(params);
    });
    renderPage(client);
    const list = await screen.findByRole('list', { name: 'Acontecimentos do projeto' });

    await userEvent.click(screen.getByRole('button', { name: 'Ver mais antigos' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('A leitura falhou.');
    expect(within(list).getAllByRole('listitem')).toHaveLength(4);
    expect(screen.getByRole('button', { name: 'Ver mais antigos' })).toBeEnabled();
  });

  it('uma fonte indisponível é dita, com o resto da linha do tempo completo', async () => {
    const broken: ProjectTimeline = {
      ...full,
      sources: full.sources.map((source) =>
        source.kind === 'decision' ? { ...source, available: false, count: 0 } : source
      )
    };
    renderPage(clientWith(() => broken).client);

    expect(await screen.findByText(/Não foi possível ler: Decisões/)).toHaveTextContent(
      'O resto da linha do tempo está completo.'
    );
    expect(screen.getByRole('list', { name: 'Acontecimentos do projeto' })).toBeInTheDocument();
  });

  it('projeto sem nada aconteceu diz isso, e filtro sem resultado diz outra coisa', async () => {
    const empty: ProjectTimeline = {
      ...full,
      events: [],
      nextBefore: null,
      sources: full.sources.map((source) => ({ ...source, count: 0 }))
    };
    const none = renderPage(clientWith(() => empty).client);
    expect(
      await screen.findByText('Ainda não aconteceu nada neste projeto que a linha do tempo conte.')
    ).toBeInTheDocument();
    none.unmount();

    renderPage(
      clientWith((params) =>
        (params.kinds as string[] | undefined)?.length ? { ...full, events: [], nextBefore: null } : full
      ).client
    );
    await userEvent.click(await screen.findByRole('button', { name: 'Sincronizações (1)' }));
    expect(await screen.findByText('Nenhum acontecimento destes tipos.')).toBeInTheDocument();
  });

  it('se o motor recusa, mostra o motivo e permite ler de novo', async () => {
    let fail = true;
    const { client } = clientWith(() =>
      fail ? new EngineError('PROJECT_NOT_AUTHORIZED', 'O projeto não foi autorizado.') : full
    );
    renderPage(client);

    expect(await screen.findByRole('alert')).toHaveTextContent('O projeto não foi autorizado.');
    fail = false;
    await userEvent.click(screen.getByRole('button', { name: /Tentar de novo/ }));

    expect(await screen.findByRole('list', { name: 'Acontecimentos do projeto' })).toBeInTheDocument();
  });

  it('sem projeto aberto manda escolher um e não pede nada', () => {
    const { client, request } = clientWith(() => full);
    renderPage(client, null);

    expect(screen.getByText(/Nenhum projeto selecionado/)).toBeInTheDocument();
    expect(request).not.toHaveBeenCalled();
  });

  it('não tem violações de acessibilidade', async () => {
    const { container } = renderPage(clientWith(() => full).client);
    await screen.findByRole('list', { name: 'Acontecimentos do projeto' });

    await expectNoA11yViolations(container);
  });
});
