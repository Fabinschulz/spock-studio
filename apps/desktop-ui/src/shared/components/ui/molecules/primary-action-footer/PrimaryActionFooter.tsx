import type { ReactNode } from 'react';

import styles from './PrimaryActionFooter.module.scss';

/**
 * Ações de navegação da etapa, presas ao rodapé da janela: continuam à vista em conteúdo longo e não cobrem o texto
 * (o conteúdo ganha o mesmo espaço, por ser sticky e não fixo).
 */
export function PrimaryActionFooter({ children }: { children: ReactNode }) {
  return (
    <div className={styles.footer} role="group" aria-label="Ações da etapa">
      {children}
    </div>
  );
}
