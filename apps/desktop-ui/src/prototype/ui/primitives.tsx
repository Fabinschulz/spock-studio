import type { ReactNode } from 'react';

import styles from './primitives.module.scss';

export type GlyphState = 'pending' | 'running' | 'done' | 'warning' | 'failed' | 'skipped';

const stateText: Record<GlyphState, string> = {
  pending: 'aguardando',
  running: 'em andamento',
  done: 'concluída',
  warning: 'com ressalva',
  failed: 'falhou',
  skipped: 'não executada'
};

/**
 * Indicador de estado. A forma muda com o estado (anel, arco girando, marca, triângulo, X, traço) e o texto
 * acessível diz o mesmo: a cor nunca é a única pista. O giro só existe enquanto algo roda de fato.
 */
export function StatusGlyph({ state, label }: { state: GlyphState; label?: string }) {
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

type Tone = 'neutral' | 'accent' | 'success' | 'warning' | 'danger';

/** Etiqueta de texto: sem borda, com fundo suave. Sempre diz o estado por extenso. */
export function Tag({ tone = 'neutral', children }: { tone?: Tone; children: ReactNode }) {
  return (
    <span className={styles.tag} data-tone={tone}>
      {children}
    </span>
  );
}

/** Seção sem caixa: só um título e uma linha fina acima. */
export function Section({
  title,
  aside,
  children,
  id
}: {
  title: string;
  aside?: ReactNode;
  children: ReactNode;
  id?: string;
}) {
  const headingId = id ? `${id}-title` : undefined;
  return (
    <section className={styles.section} aria-labelledby={headingId}>
      <header className={styles.sectionHead}>
        <h2 id={headingId} className={styles.sectionTitle}>
          {title}
        </h2>
        {aside ? <div className={styles.sectionAside}>{aside}</div> : null}
      </header>
      {children}
    </section>
  );
}

/** Par rótulo/valor em linha, para resumos. */
export function Fact({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className={styles.fact}>
      <dt>{label}</dt>
      <dd>{children}</dd>
    </div>
  );
}
