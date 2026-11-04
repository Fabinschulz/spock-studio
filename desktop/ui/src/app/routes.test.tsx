import { render, screen } from '@testing-library/react';
import { createMemoryRouter, RouterProvider } from 'react-router';
import { describe, expect, it, vi } from 'vitest';

import { ProjectSelectionProvider } from '@/features/project-selection';
import { AppProviders } from '@/shared/components';
import { executionPath, paths } from '@/shared/routing';

import { routes } from './routes';

vi.mock('@tauri-apps/api/core', () => ({ invoke: vi.fn().mockRejectedValue(new Error('sem host')) }));
vi.mock('@tauri-apps/api/event', () => ({ listen: vi.fn().mockResolvedValue(() => undefined) }));

function renderAt(path: string) {
  const router = createMemoryRouter(routes, { initialEntries: [path] });
  render(
    <AppProviders>
      <ProjectSelectionProvider picker={{ pick: vi.fn() }}>
        <RouterProvider router={router} />
      </ProjectSelectionProvider>
    </AppProviders>
  );
}

describe('rotas', () => {
  it.each([
    ['configurações', paths.settings],
    ['execução', executionPath('run-000000000000')]
  ])('no navegador, %s diz que precisa do aplicativo em vez de falhar', async (_, path) => {
    renderAt(path);

    expect(await screen.findByText('Disponível apenas no aplicativo desktop')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Voltar para o início' })).toHaveAttribute('href', '/');
  });

  it('as rotas montadas a partir de paths resolvem as páginas', async () => {
    renderAt(paths.configure);

    expect(await screen.findByRole('heading', { level: 1 })).toBeInTheDocument();
    expect(screen.queryByText('Disponível apenas no aplicativo desktop')).not.toBeInTheDocument();
  });
});
