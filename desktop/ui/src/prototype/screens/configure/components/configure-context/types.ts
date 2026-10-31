import type { FeedItem } from '@/prototype/types/feed';
import type { Participant } from '@/prototype/types/participant';
import type { SummaryFact } from '@/prototype/types/summary';

export interface ConfigureContextProps {
  summary: SummaryFact[];
  participants: Participant[];
  feed: FeedItem[];
  showActivity: boolean;
  live: boolean;
}
