import type { GlyphState } from '@/shared/components/ui';
import type { ConfigurationPlan } from '@/shared/schema';
import type { JourneyResults, JourneyStepId, Loadable } from './journey';
import type { RunView } from './run';

export interface Participant {
  id: string;
  name: string;
  glyph: GlyphState;
  state: string;
  activity?: string | null;
  note?: string;
}

export interface ParticipantsContext {
  step: JourneyStepId;
  plan: Loadable<ConfigurationPlan>;
  run: RunView | null;
  results: JourneyResults;
}
