import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { AppProviders } from '@/shared/components';
import { expectNoA11yViolations } from '@/test/a11y';

import { DiagnosticsPage } from './diagnostics-page';

function renderPage() {
  return render(
    <AppProviders>
      <MemoryRouter>
        <DiagnosticsPage />
      </MemoryRouter>
    </AppProviders>
  );
}

afterEach(() => vi.restoreAllMocks());

describe('DiagnosticsPage', () => {
  it('mostra as informações do computador e diz que o motor não existe fora do aplicativo', () => {
    renderPage();

    expect(screen.getByRole('heading', { level: 1, name: 'Diagnóstico' })).toBeInTheDocument();
    expect(screen.getByText('Aplicativo')).toBeInTheDocument();
    expect(screen.getAllByText('Indisponível na prévia do navegador')).toHaveLength(2);
  });

  it('copia as linhas da tela e confirma', async () => {
    const writeText = vi.fn().mockResolvedValue(undefined);
    Object.defineProperty(navigator, 'clipboard', { value: { writeText }, configurable: true });
    renderPage();

    await userEvent.click(screen.getByRole('button', { name: 'Copiar informações' }));

    expect(writeText).toHaveBeenCalledWith(expect.stringContaining('Aplicativo: Spock Studio v'));
    expect(await screen.findByText('Informações copiadas.')).toBeInTheDocument();
  });

  it('se o sistema recusa a cópia, diz como seguir', async () => {
    const writeText = vi.fn().mockRejectedValue(new Error('negado'));
    Object.defineProperty(navigator, 'clipboard', { value: { writeText }, configurable: true });
    renderPage();

    await userEvent.click(screen.getByRole('button', { name: 'Copiar informações' }));

    expect(await screen.findByText(/Não foi possível copiar/)).toBeInTheDocument();
  });

  it('não tem violações estruturais de acessibilidade', async () => {
    const { container } = renderPage();

    await expectNoA11yViolations(container);
  });
});
