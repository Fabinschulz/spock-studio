import type { EngineClient } from '@/shared/api';
import { errorMessage } from '@/shared/api';
import type { ContextReview } from '@/shared/schema';
import { useCallback, useState } from 'react';

import { reviewContext } from '../service/workflow-api';

export type ContextReviewState =
  | { status: 'idle' }
  | { status: 'loading' }
  | { status: 'ready'; review: ContextReview }
  | { status: 'failed'; message: string };

export function useContextReview(client: EngineClient, projectId: string) {
  const [state, setState] = useState<ContextReviewState>({ status: 'idle' });

  const review = useCallback(
    async (goal: string) => {
      setState({ status: 'loading' });
      try {
        setState({ status: 'ready', review: await reviewContext(client, projectId, goal) });
      } catch (cause) {
        setState({ status: 'failed', message: errorMessage(cause, 'Não foi possível revisar o contexto.') });
      }
    },
    [client, projectId]
  );

  return { state, review };
}
