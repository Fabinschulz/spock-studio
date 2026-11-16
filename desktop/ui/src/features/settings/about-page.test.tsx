import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { AppProviders } from '@/shared/components';
import { expectNoA11yViolations } from '@/test/a11y';

import { AboutPage, PRODUCT_MEANING } from './about-page';

const invoke = vi.fn();
vi.mock('@tauri-apps/api/core', () => ({ invoke: (...args: unknown[]) => invoke(...args) }));
vi.mock('@tauri-apps/api/event', () => ({ listen: vi.fn().mockResolvedValue(() => undefined) }));

function renderPage() {
  return render(
    <AppProviders>
      <MemoryRouter>
        <AboutPage />
      </MemoryRouter>
    </AppProviders>
  );
}

function hostWith(notices: unknown) {
  (window as unknown as Record<string, unknown>).__TAURI_INTERNALS__ = {};
  invoke.mockImplementation((command: string) => {
    if (command === 'engine_status') return Promise.resolve('ready');
    return notices instanceof Error
      ? Promise.reject({ code: 'NOTICES_NOT_FOUND', message: notices.message })
      : Promise.resolve(notices);
  });
}

beforeEach(() => {
  invoke.mockReset();
});

afterEach(() => {
  delete (window as unknown as Record<string, unknown>).__TAURI_INTERNALS__;
});

describe('Sobre', () => {
  it('apresenta o significado de SPOCK e os avisos de terceiros da instalação', async () => {
    hostWith({ text: 'MIT License\n\nCopyright (c) Exemplo', components: 484, complete: true });
    const { container } = renderPage();

    expect(screen.getByText(`SPOCK: ${PRODUCT_MEANING}.`)).toBeInTheDocument();
    expect(await screen.findByText('484 componentes de terceiros.')).toBeInTheDocument();
    expect(screen.getByLabelText('Textos de licença de terceiros')).toHaveTextContent('Copyright (c) Exemplo');
    expect(invoke).toHaveBeenCalledWith('third_party_notices');
    await expectNoA11yViolations(container);
  });

  it('avisa quando a lista de avisos ainda está incompleta', async () => {
    hostWith({ text: 'texto', components: 10, complete: false });
    renderPage();

    expect(
      await screen.findByText('10 componentes de terceiros. Alguns textos de licença ainda estão sendo reunidos.')
    ).toBeInTheDocument();
  });

  it('mostra o erro quando a instalação não traz os avisos', async () => {
    hostWith(new Error('Os avisos de terceiros não estão nesta instalação.'));
    renderPage();

    expect(
      await screen.findByText('Não foi possível ler os avisos de terceiros desta instalação.')
    ).toBeInTheDocument();
  });

  it('fora do aplicativo não chama o host', async () => {
    renderPage();

    expect(await screen.findByText('Os avisos de terceiros aparecem no aplicativo instalado.')).toBeInTheDocument();
    expect(invoke).not.toHaveBeenCalledWith('third_party_notices');
  });
});
