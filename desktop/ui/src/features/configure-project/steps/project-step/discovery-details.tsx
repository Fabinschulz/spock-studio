import type { Discovery } from '@/shared/schema';

import { DiscoverySummary } from './discovery-summary';
import { DiscoveryWarnings } from './discovery-warnings';
import { StacksSection } from './stacks-section';
import { ToolingSection } from './tooling-section';

export function DiscoveryDetails({ discovery }: { discovery: Discovery }) {
  return (
    <>
      <DiscoveryWarnings warnings={discovery.warnings} />
      <StacksSection stacks={discovery.stacks} />
      <ToolingSection tooling={discovery.tooling} />
      <DiscoverySummary discovery={discovery} />
    </>
  );
}
