import { cn } from '@/shared/utils';

import styles from './Logo.module.scss';

interface LogoProps {
  className?: string;
}

/**
 * Marca da Mariliz: dois arcos de "m" e o nó teal, a mesma do favicon do site. O tile é a tinta da marca; no tema
 * escuro ele se confunde com o fundo e o que se vê é o "m" e o nó. Decorativa: quem usa fornece o nome acessível.
 */
export function Logo({ className }: LogoProps) {
  return (
    <svg className={cn(styles.mark, className)} viewBox="0 0 32 32" aria-hidden="true" focusable="false">
      <rect className={styles.tile} width="32" height="32" rx="7" />
      <g transform="translate(3.2 1.4) scale(0.86)">
        <path
          className={styles.arches}
          d="M2 28V13.25a5.25 5.25 0 0 1 10.5 0V28M12.5 13.25a5.25 5.25 0 0 1 10.5 0V28"
        />
      </g>
      <circle className={styles.node} cx="26.4" cy="6.2" r="2.5" />
    </svg>
  );
}

/** Wordmark "mariliz" do site (monoline); os pingos dos dois "i" são os nós teal. Decorativo. */
export function Wordmark({ className }: LogoProps) {
  return (
    <svg className={cn(styles.wordmark, className)} viewBox="-2.5 -2.5 123 33.25" aria-hidden="true" focusable="false">
      <path
        className={styles.letters}
        d="M2 28V13.25a5.25 5.25 0 0 1 10.5 0V28M12.5 13.25a5.25 5.25 0 0 1 10.5 0V28M52 8v20M61 28V16.5A8.5 8.5 0 0 1 69.5 8h.5M78 13v15M87 0v28M96 13v15M104 8h14l-14 20h14"
      />
      <circle className={styles.letters} cx="42" cy="18" r="10" />
      <circle className={styles.dot} cx="78" cy="3.6" r="2.9" />
      <circle className={styles.dot} cx="96" cy="3.6" r="2.9" />
    </svg>
  );
}
