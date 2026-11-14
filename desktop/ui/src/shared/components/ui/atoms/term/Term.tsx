import type { ReactNode } from 'react';

import { Tooltip } from '../tooltip';
import { GLOSSARY, type GlossaryId } from './glossary';
import styles from './Term.module.scss';

/**
 * Um termo técnico com a explicação do glossário ao passar o mouse ou focar pelo teclado. É um botão para ser alcançável
 * por Tab; a explicação entra como descrição, então o leitor de tela lê o termo e o que ele quer dizer.
 */
export function Term({ id, children }: { id: GlossaryId; children?: ReactNode }) {
  const entry = GLOSSARY[id];

  return (
    <Tooltip title={entry.text}>
      <button type="button" className={styles.term}>
        {children ?? entry.label}
      </button>
    </Tooltip>
  );
}
