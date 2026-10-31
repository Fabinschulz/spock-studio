import type { RunFeedItem } from '../../types/run';

export interface ActivitySectionProps {
  feed: readonly RunFeedItem[];
  startedAt: number;
}
