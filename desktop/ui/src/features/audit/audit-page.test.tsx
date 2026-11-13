import auditFixture from '@contracts/fixtures/audit/audit.json';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router';
import { describe, expect, it, vi } from 'vitest';

import type { SelectedProject } from '@/features/project-selection';
import { EngineError, type EngineClient } from '@/shared/api';
import { projectAuditTrailSchema, type ProjectAuditTrail } from '@/shared/schema';
import { expectNoA11yViolations } from '@/test/a11y';

import { AuditPage } from './audit-page';

const project: SelectedProject = { projectId: 'p-000000000001', displayName: 'Loja', displayPath: '~\\Loja' };
const full = projectAuditTrailSchema.parse(auditFixture);

type Responder = (params: Record<string, unknown>) => ProjectAuditTrail | Error;

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
      <AuditPage client={client} project={selected} />
    </MemoryRouter>
  );

/** A trilha em páginas de `size`: a próxima pede o corte que a anterior devolveu. */
function paged(size: number): Responder {
  return (params) => {
    const before = params.before as string | undefined;
    const rest = before ? full.entries.filter((entry) => entry.at < before) : full.entries;
    const entries = rest.slice(0, size);
    return { ...full, entries, nextBefore: rest.length > size ? entries[entries.length - 1]!.at : null };
  };
}

describe('Auditoria', () => {
  it('pede a trilha do projeto aberto e só lê', async () => {
    const { client, request } = clientWith(() => full);
    renderPage(client);

    await screen.findByRole('list', { name: 'Entradas da auditoria' });

    expect(request).toHaveBeenCalledTimes(1);
    expect(request).toHaveBeenCalledWith('audit.read', { projectId: project.projectId, limit: 30 }, expect.anything());
  });

  it('mostra cada entrada do mais recente ao mais antigo, com a categoria e a origem', async () => {
    renderPage(clientWith(() => full).client);

    const list = await screen.findByRole('list', { name: 'Entradas da auditoria' });
    const items = within(list).getAllByRole('listitem');

    expect(items).toHaveLength(full.entries.length);
    expect(items[0]).toHaveTextContent('Fonte desvinculada do projeto: acme/app');
    expect(items[0]).toHaveTextContent('Acesso a fontes');
    expect(items[0]).toHaveTextContent('Arquivo de auditoria, linha 10');
    const applied = items.find((item) => item.textContent?.includes('Harness aplicado ao projeto'))!;
    expect(applied).toHaveTextContent('Mudanças no projeto');
    expect(applied).toHaveTextContent('Diário da execução ex-000000000001');
  });

  it('uma entrada de diário leva à execução e uma do arquivo não tem destino', async () => {
    renderPage(clientWith(() => full).client);

    const list = await screen.findByRole('list', { name: 'Entradas da auditoria' });
    const links = within(list).getAllByRole('link', { name: 'Ver execução' });

    expect(links).toHaveLength(full.entries.filter((entry) => entry.source === 'journal').length);
    expect(links[0]!.getAttribute('href')).toMatch(/^\/execucao\/ex-/);
    const fromFile = within(list).getAllByRole('listitem')[0]!;
    expect(within(fromFile).queryByRole('link')).toBeNull();
  });

  it('os filtros trazem a contagem de cada categoria e pedem só as categorias escolhidas', async () => {
    const { client, request } = clientWith((params) => {
      const categories = (params.categories as string[] | undefined) ?? [];
      return categories.length === 0
        ? full
        : { ...full, entries: full.entries.filter((entry) => categories.includes(entry.category)), nextBefore: null };
    });
    renderPage(client);

    const group = await screen.findByRole('group', { name: 'Categorias da auditoria' });
    expect(within(group).getByRole('button', { name: 'Conexões (2)' })).toHaveAttribute('aria-pressed', 'false');

    await userEvent.click(within(group).getByRole('button', { name: 'Conexões (2)' }));

    await waitFor(() =>
      expect(request).toHaveBeenLastCalledWith(
        'audit.read',
        { projectId: project.projectId, categories: ['credential'], limit: 30 },
        expect.anything()
      )
    );
    const list = await screen.findByRole('list', { name: 'Entradas da auditoria' });
    expect(within(list).getAllByRole('listitem')).toHaveLength(2);

    await userEvent.click(
      within(screen.getByRole('group', { name: 'Categorias da auditoria' })).getByRole('button', {
        name: 'Conexões (2)'
      })
    );
    await waitFor(() =>
      expect(request).toHaveBeenLastCalledWith(
        'audit.read',
        { projectId: project.projectId, limit: 30 },
        expect.anything()
      )
    );
  });

  it('"Ver mais antigas" pede a página seguinte pelo corte e junta sem repetir, e some no fim', async () => {
    const { client, request } = clientWith(paged(6));
    renderPage(client);

    const list = await screen.findByRole('list', { name: 'Entradas da auditoria' });
    expect(within(list).getAllByRole('listitem')).toHaveLength(6);

    await userEvent.click(screen.getByRole('button', { name: 'Ver mais antigas' }));
    await waitFor(() => expect(within(list).getAllByRole('listitem')).toHaveLength(12));
    expect(request).toHaveBeenLastCalledWith(
      'audit.read',
      { projectId: project.projectId, limit: 30, before: full.entries[5]!.at },
      expect.anything()
    );

    await userEvent.click(screen.getByRole('button', { name: 'Ver mais antigas' }));
    await waitFor(() => expect(within(list).getAllByRole('listitem')).toHaveLength(full.entries.length));
    expect(screen.queryByRole('button', { name: 'Ver mais antigas' })).toBeNull();
    const ids = within(list)
      .getAllByRole('listitem')
      .map((item) => item.textContent);
    expect(new Set(ids).size).toBe(ids.length);
  });

  it('se carregar mais falha, a lista continua e o motivo aparece', async () => {
    let calls = 0;
    const { client } = clientWith((params) => {
      calls += 1;
      if (calls > 1) return new EngineError('AUDIT_UNAVAILABLE', 'A leitura falhou.');
      return paged(6)(params);
    });
    renderPage(client);
    const list = await screen.findByRole('list', { name: 'Entradas da auditoria' });

    await userEvent.click(screen.getByRole('button', { name: 'Ver mais antigas' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('A leitura falhou.');
    expect(within(list).getAllByRole('listitem')).toHaveLength(6);
    expect(screen.getByRole('button', { name: 'Ver mais antigas' })).toBeEnabled();
  });

  it('uma fonte indisponível é dita, com o resto da trilha completo', async () => {
    const broken: ProjectAuditTrail = {
      ...full,
      sources: full.sources.map((source) =>
        source.source === 'audit' ? { ...source, available: false, count: 0 } : source
      )
    };
    renderPage(clientWith(() => broken).client);

    expect(await screen.findByText(/Não foi possível ler: Arquivo de auditoria/)).toHaveTextContent(
      'O resto da trilha está completo.'
    );
    expect(screen.getByRole('list', { name: 'Entradas da auditoria' })).toBeInTheDocument();
  });

  it('projeto sem nada a registrar diz isso, e filtro sem resultado diz outra coisa', async () => {
    const empty: ProjectAuditTrail = {
      ...full,
      entries: [],
      nextBefore: null,
      sources: full.sources.map((source) => ({ ...source, count: 0 })),
      categories: full.categories.map((category) => ({ ...category, count: 0 }))
    };
    const none = renderPage(clientWith(() => empty).client);
    expect(await screen.findByText('Ainda não há nada a registrar na auditoria deste projeto.')).toBeInTheDocument();
    expect(screen.queryByRole('group', { name: 'Categorias da auditoria' })?.children).toHaveLength(0);
    none.unmount();

    renderPage(
      clientWith((params) =>
        (params.categories as string[] | undefined)?.length ? { ...full, entries: [], nextBefore: null } : full
      ).client
    );
    await userEvent.click(await screen.findByRole('button', { name: 'Leituras de fontes (1)' }));
    expect(await screen.findByText('Nenhuma entrada destas categorias.')).toBeInTheDocument();
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

    expect(await screen.findByRole('list', { name: 'Entradas da auditoria' })).toBeInTheDocument();
  });

  it('sem projeto aberto manda escolher um e não pede nada', () => {
    const { client, request } = clientWith(() => full);
    renderPage(client, null);

    expect(screen.getByText(/Nenhum projeto selecionado/)).toBeInTheDocument();
    expect(request).not.toHaveBeenCalled();
  });

  it('não tem violações de acessibilidade', async () => {
    const { container } = renderPage(clientWith(() => full).client);
    await screen.findByRole('list', { name: 'Entradas da auditoria' });

    await expectNoA11yViolations(container);
  });
});
