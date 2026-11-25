import { EMPTY_COMPONENT_REQUEST } from '../domain/component-request';
import type { JourneyState } from '../types/journey';

export const initialJourneyState: JourneyState = {
  step: 'project',
  discovery: { status: 'idle' },
  aiEnvironments: [],
  allowSpecificationReinstall: false,
  updateSpecification: false,
  installSpecification: false,
  componentRequest: EMPTY_COMPONENT_REQUEST,
  plan: { status: 'idle' },
  approvedChanges: [],
  approvedCommands: [],
  resolutions: {},
  run: null,
  results: { apply: null, revert: null, validate: null }
};
