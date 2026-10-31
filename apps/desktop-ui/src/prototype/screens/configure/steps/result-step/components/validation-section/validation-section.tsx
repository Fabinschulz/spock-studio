import { Section } from '@/prototype/components/section';
import screen from '@/prototype/styles/screen.module.scss';

import { EvalRow } from '../eval-row';
import type { ValidationSectionProps } from './types';

export function ValidationSection({ evals }: ValidationSectionProps) {
  const passed = evals.filter((e) => e.status === 'passed').length;

  return (
    <Section title="Validação" id="validacao" aside={`${passed}/${evals.length} aprovadas`}>
      <ul role="list" className={screen.rows}>
        {evals.map((result) => (
          <li key={result.id}>
            <EvalRow result={result} />
          </li>
        ))}
      </ul>
    </Section>
  );
}
