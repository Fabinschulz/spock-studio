import { ErrorState, SuccessState } from '@/shared/components';

import type { Feedback, FeedbackTarget } from '../hooks/use-model-settings';

/** O retorno da última ação, só na linha onde ela aconteceu. */
export function ActionFeedback({ feedback, target }: { feedback: Feedback | null; target: FeedbackTarget }) {
  if (feedback?.target !== target) return null;
  return feedback.tone === 'success' ? (
    <SuccessState>{feedback.text}</SuccessState>
  ) : (
    <ErrorState>{feedback.text}</ErrorState>
  );
}
