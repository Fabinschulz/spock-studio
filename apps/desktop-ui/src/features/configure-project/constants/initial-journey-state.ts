import type { JourneyState } from '../types/journey';

export const initialJourneyState: JourneyState = {
  step: 'project',
  discovery: { status: 'idle' },
  aiEnvironment: null,
  allowSpecificationReinstall: false,
  updateSpecification: false,
  plan: { status: 'idle' },
  approvedChanges: [],
  approvedCommands: [],
  resolutions: {},
  run: null,
  results: { apply: null, revert: null, validate: null }
};
