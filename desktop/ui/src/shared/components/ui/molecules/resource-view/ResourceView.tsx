import { errorMessage } from '@/shared/api';
import type { Resource } from '@/shared/hooks';
import type { ReactNode } from 'react';
import { ErrorState, LoadingState } from '../page-state';

interface ResourceViewProps<T> {
  resource: Resource<T>;
  /** O que dizer enquanto lê, no gerúndio: "Lendo o portfólio…". */
  loading: string;
  /** O que dizer se a falha não vier explicada pelo motor. */
  fallbackError: string;
  onRetry?: () => void;
  children: (value: T) => ReactNode;
}

/** Os três estados de uma leitura (lendo, falhou, pronto) no mesmo lugar, para nenhuma tela escrevê-los de novo. */
export function ResourceView<T>({ resource, loading, fallbackError, onRetry, children }: ResourceViewProps<T>) {
  if (resource.status === 'loading') return <LoadingState>{loading}</LoadingState>;
  if (resource.status === 'error') {
    return <ErrorState onRetry={onRetry}>{errorMessage(resource.error, fallbackError)}</ErrorState>;
  }
  return <>{children(resource.value)}</>;
}
