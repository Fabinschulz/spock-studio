import { ActivitySection } from '@/prototype/components/activity-section';
import { ParticipantsSection } from '@/prototype/components/participants-section';
import { SummarySection } from '@/prototype/components/summary-section';

import type { ConfigureContextProps } from './types';

export function ConfigureContext({ summary, participants, feed, showActivity, live }: ConfigureContextProps) {
  return (
    <>
      <SummarySection facts={summary} />
      <ParticipantsSection participants={participants} empty="Nenhum participante ainda." />
      {showActivity ? <ActivitySection items={feed} live={live} /> : null}
    </>
  );
}
