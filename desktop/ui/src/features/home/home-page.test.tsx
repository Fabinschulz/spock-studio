import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, useLocation } from 'react-router';
import { describe, expect, it, vi } from 'vitest';

import { ProjectSelectionProvider } from '@/features/project-selection';
import { AppProviders } from '@/shared/components';

import { HomePage } from './home-page';

function Where() {
  return <p data-testid="where">{useLocation().pathname}</p>;
}

function renderHome() {
  const pick = vi.fn().mockResolvedValue(null);
  render(
    <AppProviders>
      <ProjectSelectionProvider picker={{ pick }}>
        <MemoryRouter>
          <HomePage />
          <Where />
        </MemoryRouter>
      </ProjectSelectionProvider>
    </AppProviders>
  );
  return { pick };
}

describe('Início · entradas que não exigem pasta', () => {
  it('quem acompanha projetos vai ao Portfólio sem escolher uma pasta', async () => {
    const { pick } = renderHome();

    await userEvent.click(screen.getByRole('button', { name: /Acompanhar projetos/ }));

    expect(screen.getByTestId('where')).toHaveTextContent('/portfolio');
    expect(pick).not.toHaveBeenCalled();
  });

  it('conectar ferramentas abre as Integrações sem escolher uma pasta', async () => {
    const { pick } = renderHome();

    await userEvent.click(screen.getByRole('button', { name: /Conectar ferramentas/ }));

    expect(screen.getByTestId('where')).toHaveTextContent('/integracoes');
    expect(pick).not.toHaveBeenCalled();
  });

  it('configurar um projeto pede a pasta antes de seguir', async () => {
    const { pick } = renderHome();

    await userEvent.click(screen.getByRole('button', { name: /Configurar projeto/ }));

    expect(pick).toHaveBeenCalled();
    expect(screen.getByTestId('where')).toHaveTextContent(/^\/$/);
  });
});
