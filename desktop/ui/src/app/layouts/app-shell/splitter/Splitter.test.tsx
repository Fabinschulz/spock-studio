import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import { expectNoA11yViolations } from '@/test/a11y';

import { Splitter } from './Splitter';

function renderSplitter(orientation: 'horizontal' | 'vertical') {
  const onResize = vi.fn();
  const view = render(
    <Splitter
      orientation={orientation}
      label="Tamanho do painel"
      size={300}
      min={200}
      max={600}
      step={32}
      onResize={onResize}
    />
  );
  return { onResize, ...view };
}

describe('Splitter', () => {
  it('diz o que ajusta, o tamanho atual e os limites', () => {
    renderSplitter('vertical');

    const splitter = screen.getByRole('separator', { name: 'Tamanho do painel' });
    expect(splitter).toHaveAttribute('aria-orientation', 'vertical');
    expect(splitter).toHaveAttribute('aria-valuenow', '300');
    expect(splitter).toHaveAttribute('aria-valuemin', '200');
    expect(splitter).toHaveAttribute('aria-valuemax', '600');
  });

  it('na barra horizontal, seta para cima aumenta e para baixo diminui', async () => {
    const { onResize } = renderSplitter('horizontal');
    screen.getByRole('separator').focus();

    await userEvent.keyboard('{ArrowUp}{ArrowDown}');

    expect(onResize.mock.calls).toEqual([[332], [268]]);
  });

  it('na barra vertical, o painel cresce para a esquerda: seta para a esquerda aumenta', async () => {
    const { onResize } = renderSplitter('vertical');
    screen.getByRole('separator').focus();

    await userEvent.keyboard('{ArrowLeft}{ArrowRight}');

    expect(onResize.mock.calls).toEqual([[332], [268]]);
  });

  it('Home e End levam ao mínimo e ao máximo, e as setas da outra direção não fazem nada', async () => {
    const { onResize } = renderSplitter('vertical');
    screen.getByRole('separator').focus();

    await userEvent.keyboard('{Home}{End}{ArrowUp}{ArrowDown}');

    expect(onResize.mock.calls).toEqual([[200], [600]]);
  });

  it('é alcançável pelo teclado e não tem violações de acessibilidade', async () => {
    const { container } = renderSplitter('horizontal');

    await userEvent.tab();

    expect(screen.getByRole('separator')).toHaveFocus();
    await expectNoA11yViolations(container);
  });
});
