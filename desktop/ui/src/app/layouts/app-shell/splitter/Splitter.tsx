import { cn } from '@/shared/utils';
import type { KeyboardEvent, PointerEvent } from 'react';

import styles from './Splitter.module.scss';

interface SplitterProps {
  /** A direção da barra: `horizontal` ajusta a altura de um painel de baixo; `vertical`, a largura de um da direita. */
  orientation: 'horizontal' | 'vertical';
  label: string;
  size: number;
  min: number;
  max: number;
  step: number;
  /** Recebe o tamanho pedido; quem guarda o tamanho é quem o limita. */
  onResize: (size: number) => void;
}

// Os dois painéis crescem em direção ao conteúdo: o de baixo para cima, o da direita para a esquerda.
const KEYS = {
  horizontal: { grow: 'ArrowUp', shrink: 'ArrowDown' },
  vertical: { grow: 'ArrowLeft', shrink: 'ArrowRight' }
} as const;

/** Arrastar ou usar as setas: quem não usa mouse também ajusta o tamanho. Home e End levam ao mínimo e ao máximo. */
export function Splitter({ orientation, label, size, min, max, step, onResize }: SplitterProps) {
  const position = (event: { clientX: number; clientY: number }) =>
    orientation === 'horizontal' ? event.clientY : event.clientX;

  const onPointerDown = (event: PointerEvent<HTMLDivElement>) => {
    const start = position(event);
    const startSize = size;
    const handle = event.currentTarget;
    handle.setPointerCapture(event.pointerId);
    const move = (moved: globalThis.PointerEvent) => onResize(startSize + (start - position(moved)));
    const stop = () => {
      handle.removeEventListener('pointermove', move);
      handle.removeEventListener('pointerup', stop);
    };
    handle.addEventListener('pointermove', move);
    handle.addEventListener('pointerup', stop);
  };

  const onKeyDown = (event: KeyboardEvent<HTMLDivElement>) => {
    const next: Record<string, number> = {
      [KEYS[orientation].grow]: size + step,
      [KEYS[orientation].shrink]: size - step,
      Home: min,
      End: max
    };
    if (!(event.key in next)) return;
    event.preventDefault();
    onResize(next[event.key]!);
  };

  /* eslint-disable jsx-a11y/no-noninteractive-element-interactions, jsx-a11y/no-noninteractive-tabindex --
     o padrão "window splitter" do WAI-ARIA é um separator focável que se move pelo teclado; a regra não o conhece. */
  return (
    <div
      role="separator"
      aria-orientation={orientation}
      aria-label={label}
      aria-valuenow={size}
      aria-valuemin={min}
      aria-valuemax={max}
      tabIndex={0}
      className={cn(styles.splitter, styles[orientation])}
      onPointerDown={onPointerDown}
      onKeyDown={onKeyDown}
    />
  );
  /* eslint-enable jsx-a11y/no-noninteractive-element-interactions, jsx-a11y/no-noninteractive-tabindex */
}
