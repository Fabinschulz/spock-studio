import { participantsOf } from '../../domain/participants';
import { buildSummaryFacts } from '../../domain/summary-facts';
import { ActivitySection } from '../activity-section';
import { ParticipantsSection } from '../participants-section';
import { SummarySection } from '../summary-section';
import type { ContextPanelProps } from './types';

export function ContextPanel(props: ContextPanelProps) {
  const { run } = props;

  return (
    <>
      <SummarySection facts={buildSummaryFacts(props)} />
      <ParticipantsSection participants={participantsOf(props)} />
      {run ? <ActivitySection feed={run.feed} startedAt={run.startedAt} /> : null}
    </>
  );
}
