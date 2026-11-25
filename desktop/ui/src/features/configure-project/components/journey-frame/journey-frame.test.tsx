import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router';
import { describe, expect, it, vi } from 'vitest';

import { AppProviders } from '@/shared/components';
import { ContentAreaProvider } from '@/shared/hooks';
import { expectNoA11yViolations } from '@/test/a11y';

import { JourneyFrame } from './journey-frame';

/** A área de conteúdo do shell, com a largura que o teste quiser (o jsdom não faz layout). */
function contentArea(width: number) {
  const element = document.createElement('main');
  Object.defineProperty(element, 'clientWidth', { configurable: true, get: () => width });
  return { current: element };
}

function renderFrame(props: Partial<React.ComponentProps<typeof JourneyFrame>> = {}, areaWidth = 600) {
  return render(
    <AppProviders>
      <MemoryRouter>
        <ContentAreaProvider value={contentArea(areaWidth)}>
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
        </ContentAreaProvider>
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
      'Voltar para a etapa Ambiente de IA'
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

  it('nasce aberto quando cabe ao lado do conteúdo, e então não toma o foco', () => {
    renderFrame({}, 1100);

    const panel = screen.getByRole('complementary', { name: 'Contexto da configuração' });
    expect(panel).not.toHaveFocus();
    expect(toggle()).toHaveAttribute('aria-expanded', 'true');
  });

  it('nasce fechado quando cobriria o conteúdo, mesmo numa janela larga', () => {
    // A janela pode ser larga e a área de conteúdo estreita: o menu da área e o painel de atividade também ocupam largura.
    renderFrame({}, 900);

    expect(screen.queryByRole('complementary', { name: 'Contexto da configuração' })).not.toBeInTheDocument();
    expect(toggle()).toHaveAttribute('aria-expanded', 'false');
  });

  it('quando cabe ao lado, a pessoa ainda pode fechá-lo', async () => {
    renderFrame({}, 1100);

    await userEvent.click(toggle());

    expect(screen.queryByRole('complementary', { name: 'Contexto da configuração' })).not.toBeInTheDocument();
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
