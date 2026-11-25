import timelineFixture from '@contracts/fixtures/timeline/timeline.json';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router';
import { describe, expect, it, vi } from 'vitest';

import type { SelectedProject } from '@/features/project-selection';
import { EngineError, type EngineClient } from '@/shared/api';
import { AppProviders } from '@/shared/components';
import { projectTimelineSchema, type ProjectTimeline } from '@/shared/schema';
import { expectNoA11yViolations } from '@/test/a11y';

import { ProjectFeed } from './project-feed';

const loja: SelectedProject = { projectId: 'p-000000000001', displayName: 'Loja', displayPath: '~\\Loja' };
const portal: SelectedProject = { projectId: 'p-000000000002', displayName: 'Portal', displayPath: '~\\Portal' };
const full = projectTimelineSchema.parse(timelineFixture);

function clientWith(answer: (projectId: string) => ProjectTimeline | Error) {
  const request = vi.fn(
    async (_method: string, params: Record<string, unknown>, schema: { parse(value: unknown): unknown }) => {
      const result = answer(String(params.projectId));
      if (result instanceof Error) throw result;
      return schema.parse(result);
    }
  );
  return { client: { request, onEvent: vi.fn() } as unknown as EngineClient, request };
}

const feed = (client: EngineClient, project: SelectedProject | null) => (
  <AppProviders>
    <MemoryRouter>
      <ProjectFeed client={client} project={project} />
    </MemoryRouter>
  </AppProviders>
);

describe('Atividade · o que aconteceu no projeto', () => {
  it('sem projeto aberto diz o que fazer e não pergunta nada ao motor', () => {
    const { client, request } = clientWith(() => full);
    render(feed(client, null));

    expect(screen.getByText('Abra um projeto para ver o que aconteceu nele.')).toBeInTheDocument();
    expect(request).not.toHaveBeenCalled();
  });

  it('lê a linha do tempo do projeto aberto, sem filtro, e mostra do mais recente ao mais antigo', async () => {
    const { client, request } = clientWith(() => full);
    render(feed(client, loja));

    const list = await screen.findByRole('list', { name: 'Acontecimentos de Loja' });
    const items = within(list).getAllByRole('listitem');
    expect(items).toHaveLength(full.events.length);
    expect(items[0]).toHaveTextContent('Decisão: Adiar o marco em duas semanas');
    expect(request).toHaveBeenCalledWith('timeline.read', { projectId: loja.projectId, limit: 30 }, expect.anything());
  });

  it('mostra só os primeiros e leva à linha do tempo completa quando há mais', async () => {
    const many = { ...full, events: Array.from({ length: 20 }, (_, n) => ({ ...full.events[0]!, id: `e-${n}` })) };
    render(feed(clientWith(() => many).client, loja));

    const list = await screen.findByRole('list', { name: 'Acontecimentos de Loja' });
    expect(within(list).getAllByRole('listitem')).toHaveLength(12);
    expect(screen.getByRole('link', { name: 'Ver a linha do tempo completa' })).toHaveAttribute(
      'href',
      '/linha-do-tempo'
    );
  });

  it('com tudo à vista, não oferece o link para o resto', async () => {
    render(feed(clientWith(() => ({ ...full, nextBefore: null })).client, loja));

    await screen.findByRole('list', { name: 'Acontecimentos de Loja' });
    expect(screen.queryByRole('link', { name: 'Ver a linha do tempo completa' })).not.toBeInTheDocument();
  });

  it('projeto em que nada aconteceu diz isso, com o nome dele', async () => {
    render(feed(clientWith(() => ({ ...full, events: [], nextBefore: null })).client, loja));

    expect(await screen.findByText(/Ainda não aconteceu nada em Loja/)).toBeInTheDocument();
  });

  it('se o motor recusa, mostra o motivo e permite ler de novo', async () => {
    let fail = true;
    const { client } = clientWith(() =>
      fail ? new EngineError('PROJECT_NOT_FOUND', 'Projeto não autorizado.') : full
    );
    render(feed(client, loja));

    expect(await screen.findByRole('alert')).toHaveTextContent('Projeto não autorizado.');
    fail = false;
    await userEvent.click(screen.getByRole('button', { name: 'Tentar de novo' }));

    expect(await screen.findByRole('list', { name: 'Acontecimentos de Loja' })).toBeInTheDocument();
  });

  it('ao trocar de projeto, lê o outro e não mostra nada do anterior', async () => {
    const other = { ...full, projectId: portal.projectId, events: [{ ...full.events[0]!, title: 'Só do Portal' }] };
    const { client } = clientWith((projectId) => (projectId === portal.projectId ? other : full));
    const view = render(feed(client, loja));
    await screen.findByRole('list', { name: 'Acontecimentos de Loja' });

    view.rerender(feed(client, portal));

    expect(screen.queryByRole('list', { name: 'Acontecimentos de Loja' })).not.toBeInTheDocument();
    const list = await screen.findByRole('list', { name: 'Acontecimentos de Portal' });
    expect(within(list).getByText('Só do Portal')).toBeInTheDocument();
  });

  it('não tem violações de acessibilidade', async () => {
    const { container } = render(feed(clientWith(() => full).client, loja));
    await screen.findByRole('list', { name: 'Acontecimentos de Loja' });

    await expectNoA11yViolations(container);
  });
});
