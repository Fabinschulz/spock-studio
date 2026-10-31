import { CopyIcon } from '@synthra.io/ui-kit';
import { useState } from 'react';

import styles from './CommandDisplay.module.scss';

interface CommandDisplayProps {
  /** O comando exato, como seria executado. Só é exibido e copiado: nada aqui executa. */
  command: string;
  kind?: string;
  description?: string;
}

type CopyState = 'idle' | 'copied' | 'failed';

export function CommandDisplay({ command, kind, description }: CommandDisplayProps) {
  const [copy, setCopy] = useState<CopyState>('idle');

  const copyCommand = async () => {
    try {
      await navigator.clipboard.writeText(command);
      setCopy('copied');
    } catch {
      setCopy('failed');
    }
  };

  return (
    <div className={styles.command}>
      {kind ? <span className={styles.kind}>{kind}</span> : null}
      <div className={styles.body}>
        <code className={styles.code}>{command}</code>
        {description ? <span className={styles.description}>{description}</span> : null}
      </div>
      <button
        type="button"
        className={styles.copy}
        onClick={() => void copyCommand()}
        aria-label={`Copiar o comando ${command}`}
      >
        <CopyIcon fontSize="small" aria-hidden="true" />
        <span>{copy === 'copied' ? 'Copiado' : 'Copiar'}</span>
      </button>
      <span role="status" className="visually-hidden">
        {copy === 'copied'
          ? 'Comando copiado.'
          : copy === 'failed'
            ? 'Não foi possível copiar. Selecione o comando e copie com Ctrl+C.'
            : ''}
      </span>
    </div>
  );
}
