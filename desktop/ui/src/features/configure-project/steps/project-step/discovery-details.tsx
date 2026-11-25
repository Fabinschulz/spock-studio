import { Disclosure } from '@/shared/components/ui';
import type { Discovery } from '@/shared/schema';

import { DiscoverySummary, SystemRow } from './discovery-summary';
import { DiscoveryWarnings } from './discovery-warnings';
import { StacksSection } from './stacks-section';
import { ToolingSection } from './tooling-section';

/** O estado vem primeiro; stack, comandos e sistema ficam recolhidos, mas sempre à mão. */
export function DiscoveryDetails({ discovery }: { discovery: Discovery }) {
  return (
    <>
      <DiscoverySummary discovery={discovery} />
      <DiscoveryWarnings warnings={discovery.warnings} />
      <Disclosure summary="Stack, comandos e sistema">
        <StacksSection stacks={discovery.stacks} />
        <ToolingSection tooling={discovery.tooling} />
        <SystemRow tools={discovery.tools} />
      </Disclosure>
    </>
  );
}
