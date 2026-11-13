import { errorMessage } from '@/shared/api';
import type { Resource } from '@/shared/hooks';
import type { ReactNode } from 'react';
import { ErrorState, LoadingState } from '../page-state';

interface ResourceViewProps<T> {
  resource: Resource<T>;
  /** O que dizer enquanto lê, no gerúndio: "Lendo o portfólio…". */
  loading: string;
  fallbackError: string;
  /** Sem isto, vale o `reload` da própria leitura (se ela tiver um). */
  onRetry?: () => void;
  children: (value: T) => ReactNode;
}

export function ResourceView<T>({ resource, loading, fallbackError, onRetry, children }: ResourceViewProps<T>) {
  if (resource.status === 'loading') return <LoadingState>{loading}</LoadingState>;
  if (resource.status === 'error') {
    return <ErrorState onRetry={onRetry ?? resource.reload}>{errorMessage(resource.error, fallbackError)}</ErrorState>;
  }
  return <>{children(resource.value)}</>;
}
