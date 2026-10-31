import { Glyph } from '@/shared/components/ui';

import { Notice } from '@/prototype/components/notice';
import { Section } from '@/prototype/components/section';
import { COMMAND_EFFECT_TEXT } from '@/prototype/constants/review';
import { plan } from '@/prototype/data/fixtures';

export function SpecificationSection() {
  const install = plan.commands[0];

  return (
    <Section title="Estrutura de especificação" id="estrutura">
      <Notice icon={<Glyph name="layers" size={16} />}>
        <p>
          <strong>Será instalada pelo instalador oficial.</strong>
        </p>
        <p>
          {install
            ? `Esta ação ${install.effects.map((e) => COMMAND_EFFECT_TEXT[e]).join(', ')}. Ela só roda se você aprovar na revisão.`
            : 'Nenhuma instalação é necessária.'}
        </p>
      </Notice>
    </Section>
  );
}
