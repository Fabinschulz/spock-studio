import { useCopyToClipboard } from '@/shared/hooks';
import { CopyIcon } from '@synthra.io/ui-kit';

import styles from './CommandDisplay.module.scss';

interface CommandDisplayProps {
  /** O comando exato, como seria executado. Só é exibido e copiado: nada aqui executa. */
  command: string;
  kind?: string;
}

export function CommandDisplay({ command, kind }: CommandDisplayProps) {
  const { state: copy, copy: copyText } = useCopyToClipboard();

  return (
    <div className={styles.command}>
      {kind ? <span className={styles.kind}>{kind}</span> : null}
      <div className={styles.body}>
        <code className={styles.code}>{command}</code>
      </div>
      <button
        type="button"
        className={styles.copy}
        onClick={() => void copyText(command)}
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
