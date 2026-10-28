import { ChevronDownGlyph } from './glyphs';
import styles from './panels.module.scss';

/** Hexágono com a marca de verificação: o glifo do método SDD. Decorativo. */
export function TlcGlyph({ size = 18 }: { size?: number }) {
  return (
    <svg width={size} height={size} viewBox="0 0 24 24" fill="none" aria-hidden="true" focusable="false">
      <path d="M12 2.5 20 7v10l-8 4.5L4 17V7l8-4.5Z" stroke="currentColor" strokeWidth="1.8" strokeLinejoin="round" />
      <path d="m8.5 12.2 2.4 2.4 4.6-5" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" />
    </svg>
  );
}

/**
 * Seletor do método. Nesta fase só existe o TLC: o seletor tem uma única opção, e dizer isso é mais honesto que
 * esconder o controle (não há outro kit para escolher).
 */
export function MethodBadge() {
  return (
    <span className={styles.methodBadge}>
      <TlcGlyph />
      <select className={styles.methodSelect} aria-label="Método de especificação" defaultValue="tlc">
        <option value="tlc">TLC (padrão)</option>
      </select>
      <ChevronDownGlyph />
    </span>
  );
}
