import { useCopyToClipboard } from '@/shared/hooks';
import { Button, CopyIcon } from '@mariliz/ui-kit';

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
      <Button
        variant="text"
        size="small"
        className={styles.copy}
        startIcon={<CopyIcon fontSize="small" aria-hidden="true" />}
        onClick={() => void copyText(command)}
        aria-label={`Copiar o comando ${command}`}
      >
        {copy === 'copied' ? 'Copiado' : 'Copiar'}
      </Button>
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
