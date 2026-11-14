import type { KeyboardEvent, PointerEvent } from 'react';

import { PANEL_HEIGHT } from './use-bottom-panel';
import styles from './BottomPanel.module.scss';

/** Arrastar ou usar as setas: quem não usa mouse também ajusta a altura. */
export function PanelResizer({ height, onResize }: { height: number; onResize: (height: number) => void }) {
  const onPointerDown = (event: PointerEvent<HTMLDivElement>) => {
    const startY = event.clientY;
    const startHeight = height;
    const handle = event.currentTarget;
    handle.setPointerCapture(event.pointerId);
    const move = (moved: globalThis.PointerEvent) => onResize(startHeight + (startY - moved.clientY));
    const stop = () => {
      handle.removeEventListener('pointermove', move);
      handle.removeEventListener('pointerup', stop);
    };
    handle.addEventListener('pointermove', move);
    handle.addEventListener('pointerup', stop);
  };

  const onKeyDown = (event: KeyboardEvent<HTMLDivElement>) => {
    const next: Record<string, number> = {
      ArrowUp: height + PANEL_HEIGHT.step,
      ArrowDown: height - PANEL_HEIGHT.step,
      Home: PANEL_HEIGHT.min,
      End: window.innerHeight
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
      aria-orientation="horizontal"
      aria-label="Altura do painel inferior"
      aria-valuenow={height}
      aria-valuemin={PANEL_HEIGHT.min}
      tabIndex={0}
      className={styles.resizer}
      onPointerDown={onPointerDown}
      onKeyDown={onKeyDown}
    />
  );
  /* eslint-enable jsx-a11y/no-noninteractive-element-interactions, jsx-a11y/no-noninteractive-tabindex */
}
