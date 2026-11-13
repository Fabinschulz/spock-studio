import { pathTo } from '@/shared/routing';
import { Button } from '@mariliz/ui-kit';
import type { ReactNode } from 'react';
import { Link } from 'react-router';
import styles from './PageState.module.scss';

/** Região de estado: o leitor de tela anuncia sem tirar o foco de onde a pessoa está. */
export function LoadingState({ children }: { children: ReactNode }) {
  return (
    <p role="status" className={styles.loading}>
      {children}
    </p>
  );
}

/** Só se escreve quando o motor confirmou o efeito: sucesso nunca é presumido a partir de a chamada não ter falhado. */
export function SuccessState({ children }: { children: ReactNode }) {
  return (
    <p role="status" className={styles.success}>
      {children}
    </p>
  );
}

interface ErrorStateProps {
  children: ReactNode;
  onRetry?: () => void;
}

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

/** Não é erro e não é "carregando": dizer qual dos três é o que evita a pessoa esperar por algo que não vem. */
export function EmptyState({ children }: { children: ReactNode }) {
  return <p className={styles.empty}>{children}</p>;
}

export function NoProjectState() {
  return (
    <EmptyState>
      Nenhum projeto selecionado. Escolha um no seletor do topo ou na <Link to={pathTo('home')}>tela inicial</Link>.
    </EmptyState>
  );
}
