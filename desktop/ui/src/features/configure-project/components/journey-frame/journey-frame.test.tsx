import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router';
import { describe, expect, it, vi } from 'vitest';

import { AppProviders } from '@/shared/components';
import { expectNoA11yViolations } from '@/test/a11y';

import { JourneyFrame } from './journey-frame';

function renderFrame(props: Partial<React.ComponentProps<typeof JourneyFrame>> = {}) {
  return render(
    <AppProviders>
      <MemoryRouter>
        <JourneyFrame
          step="plan"
          panel={
            <>
              <p>resumo</p>
              <button type="button">dentro do painel</button>
            </>
          }
          {...props}
        >
          <p>conteúdo da etapa</p>
        </JourneyFrame>
      </MemoryRouter>
    </AppProviders>
  );
}

const toggle = () => screen.getByRole('button', { name: /painel de contexto/ });

describe('JourneyFrame · stepper', () => {
  it('com onSelectStep, volta a uma etapa concluída; a atual e as futuras não são controles', async () => {
    const onSelectStep = vi.fn();
    renderFrame({ onSelectStep });
    const steps = screen.getByRole('list', { name: 'Etapas da configuração' });

    const buttons = within(steps).getAllByRole('button');
    expect(buttons.map((button) => button.getAttribute('aria-label'))).toEqual([
      'Voltar para a etapa Projeto',
      'Voltar para a etapa Modelo e ambiente'
    ]);
    await userEvent.click(buttons[1]!);

    expect(onSelectStep).toHaveBeenCalledExactlyOnceWith('model');
  });

  it('sem onSelectStep, o stepper só informa: nenhuma etapa é controle', () => {
    renderFrame();

    expect(within(screen.getByRole('list', { name: 'Etapas da configuração' })).queryByRole('button')).toBeNull();
  });
});

describe('JourneyFrame · painel de contexto', () => {
  it('abrir pelo botão leva o foco ao painel; Esc o fecha e devolve o foco ao botão', async () => {
    renderFrame();
    await userEvent.click(toggle());

    const panel = screen.getByRole('complementary', { name: 'Contexto da configuração' });
    expect(panel).toHaveFocus();

    await userEvent.keyboard('{Escape}');

    expect(screen.queryByRole('complementary', { name: 'Contexto da configuração' })).not.toBeInTheDocument();
    expect(toggle()).toHaveFocus();
    expect(toggle()).toHaveAttribute('aria-expanded', 'false');
  });

  it('Esc também fecha de um controle de dentro do painel', async () => {
    renderFrame();
    await userEvent.click(toggle());

    await userEvent.click(screen.getByRole('button', { name: 'dentro do painel' }));
    await userEvent.keyboard('{Escape}');

    expect(screen.queryByRole('complementary', { name: 'Contexto da configuração' })).not.toBeInTheDocument();
    expect(toggle()).toHaveFocus();
  });

  it('Esc fora do painel não o fecha', async () => {
    renderFrame();
    await userEvent.click(toggle());

    await userEvent.click(screen.getByText('conteúdo da etapa'));
    await userEvent.keyboard('{Escape}');

    expect(screen.getByRole('complementary', { name: 'Contexto da configuração' })).toBeInTheDocument();
  });

  it('o painel que já nasce aberto, em janela larga, não toma o foco', () => {
    const original = window.matchMedia;
    window.matchMedia = vi.fn().mockReturnValue({ matches: true }) as unknown as typeof window.matchMedia;
    try {
      renderFrame();

      const panel = screen.getByRole('complementary', { name: 'Contexto da configuração' });
      expect(panel).not.toHaveFocus();
      expect(toggle()).toHaveAttribute('aria-expanded', 'true');
    } finally {
      window.matchMedia = original;
    }
  });

  it('fechar e abrir de novo leva o foco ao painel outra vez', async () => {
    renderFrame();
    await userEvent.click(toggle());
    await userEvent.click(toggle());
    expect(screen.queryByRole('complementary')).not.toBeInTheDocument();

    await userEvent.click(toggle());

    expect(screen.getByRole('complementary', { name: 'Contexto da configuração' })).toHaveFocus();
  });

  it('não tem violações estruturais de acessibilidade com o painel aberto', async () => {
    const { container } = renderFrame({ onSelectStep: () => undefined });
    await userEvent.click(toggle());

    await expectNoA11yViolations(container);
  });
});
