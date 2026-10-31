import type { Participant } from '@/prototype/types/participant';

export interface ParticipantsSectionProps {
  participants: Participant[];
  empty?: string;
}
