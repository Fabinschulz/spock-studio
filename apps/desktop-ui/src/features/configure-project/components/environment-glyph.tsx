import styles from './panels.module.scss';

const CLAUDE_PATHS = ['M12 3v18M3 12h18M5.6 5.6l12.8 12.8M18.4 5.6 5.6 18.4'];
const CURSOR_PATHS = ['M12 2.8 20 7.4v9.2l-8 4.6-8-4.6V7.4l8-4.6Z', 'm9 9 7 2.6-3 1.4-1.4 3L9 9Z'];
const CODEX_PATHS = ['M12 3.2 19 7.2v8L12 20.8 5 16.8v-8l7-4Z', 'M12 3.2v8.2l7 4M12 11.4l-7 4M5 7.2l7 4.2 7-4.2'];

function pathsOf(id: string): readonly string[] {
  switch (id) {
    case 'claude-code':
      return CLAUDE_PATHS;
    case 'cursor':
      return CURSOR_PATHS;
    default:
      return CODEX_PATHS;
  }
}

/** Marcas redesenhadas em traço simples: a Synthra não traz os logotipos oficiais. Decorativas. */
export function EnvironmentGlyph({ id }: { id: string }) {
  return (
    <span className={styles.envGlyph} aria-hidden="true">
      <svg width={20} height={20} viewBox="0 0 24 24" fill="none" focusable="false">
        <g stroke="currentColor" strokeWidth={1.7} strokeLinecap="round" strokeLinejoin="round">
          {pathsOf(id).map((d) => (
            <path key={d} d={d} />
          ))}
        </g>
      </svg>
    </span>
  );
}
