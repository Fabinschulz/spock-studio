import { Section } from '@/prototype/components/section';
import screen from '@/prototype/styles/screen.module.scss';

import type { ValidationsSectionProps } from './types';

const VISIBLE_VALIDATIONS = 3;

export function ValidationsSection({ validations }: ValidationsSectionProps) {
  return (
    <Section title="Depois de aplicar, a validação executa" id="validacoes">
      <ul role="list" className={screen.stack}>
        {validations.slice(0, VISIBLE_VALIDATIONS).map((validation) => (
          <li key={validation} className={screen.rowMeta}>
            {validation}
          </li>
        ))}
      </ul>
    </Section>
  );
}
