import { pathTo } from '@/shared/routing';
import { Button } from '@mariliz/ui-kit';
import type { ReactNode } from 'react';
import { Link } from 'react-router';
import styles from './PageState.module.scss';

/** Uma leitura em andamento. É uma região de estado: o leitor de tela anuncia sem tirar o foco de onde a pessoa está. */
export function LoadingState({ children }: { children: ReactNode }) {
  return (
    <p role="status" className={styles.loading}>
      {children}
    </p>
  );
}

/**
 * Uma ação que deu certo, dita perto de onde foi feita. Só se escreve quando o motor confirmou o efeito: sucesso nunca é
 * presumido a partir de a chamada não ter falhado.
 */
export function SuccessState({ children }: { children: ReactNode }) {
  return (
    <p role="status" className={styles.success}>
      {children}
    </p>
  );
}

interface ErrorStateProps {
  children: ReactNode;
  /** Quando a leitura pode ser refeita, oferece "Tentar de novo" ao lado do erro. */
  onRetry?: () => void;
}

/** Uma falha, dita perto de onde aconteceu. O texto vem do motor quando ele explica o erro. */
export function ErrorState({ children, onRetry }: ErrorStateProps) {
  return (
    <div role="alert" className={styles.error}>
      <p className={styles.errorText}>{children}</p>
      {onRetry ? (
        <Button type="button" variant="text" size="small" onClick={onRetry}>
          Tentar de novo
        </Button>
      ) : null}
    </div>
  );
}

/**
 * Nada para mostrar ainda, com o motivo e, quando existe, o próximo passo. Não é erro e não é "carregando": dizer qual
 * dos três é o que evita a pessoa esperar por algo que não vem.
 */
export function EmptyState({ children }: { children: ReactNode }) {
  return <p className={styles.empty}>{children}</p>;
}

/** A tela precisa de um projeto aberto. Diz onde escolher, em vez de ficar em branco. */
export function NoProjectState() {
  return (
    <EmptyState>
      Nenhum projeto selecionado. Escolha um no seletor do topo ou na <Link to={pathTo('home')}>tela inicial</Link>.
    </EmptyState>
  );
}
