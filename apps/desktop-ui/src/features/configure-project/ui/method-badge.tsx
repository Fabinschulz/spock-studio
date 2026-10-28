import styles from './panels.module.scss';

/** Método SDD fixo desta fase, como informação (não é um seletor: não há outro kit). */
export function MethodBadge() {
  return (
    <span className={styles.methodBadge}>
      <svg width="18" height="18" viewBox="0 0 24 24" fill="none" aria-hidden="true" focusable="false">
        <path d="M12 2.5 20 7v10l-8 4.5L4 17V7l8-4.5Z" stroke="currentColor" strokeWidth="1.8" strokeLinejoin="round" />
        <path d="m8.5 12.2 2.4 2.4 4.6-5" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" />
      </svg>
      <span>
        <span className="visually-hidden">Método de especificação: </span>TLC (padrão)
      </span>
    </span>
  );
}
