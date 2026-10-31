import { stateText } from './constants';
import styles from './StatusGlyph.module.scss';
import type { StatusGlyphProps } from './types';

export function StatusGlyph({ state, label }: StatusGlyphProps) {
  return (
    <span className={styles.glyph} data-state={state} role="img" aria-label={label ?? stateText[state]}>
      <svg width="18" height="18" viewBox="0 0 18 18" fill="none" aria-hidden="true">
        {state === 'pending' ? (
          <circle cx="9" cy="9" r="6.5" className={styles.stroke} strokeDasharray="2 2.6" />
        ) : null}
        {state === 'running' ? (
          <>
            <circle cx="9" cy="9" r="6.5" className={styles.track} />
            <path d="M9 2.5a6.5 6.5 0 0 1 6.5 6.5" className={styles.arc} />
          </>
        ) : null}
        {state === 'done' ? (
          <>
            <circle cx="9" cy="9" r="7" className={styles.fill} />
            <path d="m5.8 9.2 2.3 2.3 4.2-4.6" className={styles.mark} />
          </>
        ) : null}
        {state === 'warning' ? (
          <>
            <path d="M9 2.8 16 15H2z" className={styles.fillWarn} />
            <path d="M9 7.6v3.2M9 12.6v.01" className={styles.mark} />
          </>
        ) : null}
        {state === 'failed' ? (
          <>
            <circle cx="9" cy="9" r="7" className={styles.fillDanger} />
            <path d="m6.4 6.4 5.2 5.2M11.6 6.4l-5.2 5.2" className={styles.mark} />
          </>
        ) : null}
        {state === 'skipped' ? <path d="M5 9h8" className={styles.stroke} /> : null}
      </svg>
    </span>
  );
}
