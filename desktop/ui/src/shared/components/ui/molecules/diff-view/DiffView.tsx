import { cn } from '@/shared/utils';
import styles from './DiffView.module.scss';

interface DiffViewProps {
  diff: string;
  label: string;
}

type LineKind = 'add' | 'del' | 'hunk' | 'meta' | 'ctx';

function kindOf(line: string): LineKind {
  if (line.startsWith('+++') || line.startsWith('---')) return 'meta';
  if (line.startsWith('@@')) return 'hunk';
  if (line.startsWith('+')) return 'add';
  if (line.startsWith('-')) return 'del';
  return 'ctx';
}

/**
 * Diff unificado. Cada linha conserva o prefixo (+, -, espaço), então o significado não depende de cor.
 * O conteúdo vem do motor já com linhas de possível segredo omitidas.
 */
export function DiffView({ diff, label }: DiffViewProps) {
  const lines = diff.replace(/\r\n/g, '\n').replace(/\n$/, '').split('\n');

  return (
    // Bloco rolável: precisa de foco para quem navega só pelo teclado.
    // eslint-disable-next-line jsx-a11y/no-noninteractive-tabindex
    <pre className={styles.diff} tabIndex={0} aria-label={`Diferenças em ${label}`}>
      {lines.map((line, i) => (
        <span key={i} className={cn(styles.line, styles[kindOf(line)])}>
          {line === '' ? ' ' : line}
          {'\n'}
        </span>
      ))}
    </pre>
  );
}
