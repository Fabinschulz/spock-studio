import type { ConfigureState } from '@/prototype/types/journey';

export const countApprovals = ({ approved, commands, resolutions }: ConfigureState): number =>
  approved.length + commands.length + Object.values(resolutions).filter((r) => r === 'overwrite').length;
