import { Section } from '@/prototype/components/section';
import { Tag } from '@/prototype/components/tag';
import { AI_ENVIRONMENT_OPTIONS } from '@/prototype/constants/environments';
import { discovery } from '@/prototype/data/fixtures';
import screen from '@/prototype/styles/screen.module.scss';

import type { EnvironmentSectionProps } from './types';

export function EnvironmentSection({ env, onEnv, disabled }: EnvironmentSectionProps) {
  return (
    <Section title="Ambiente de IA" id="ambiente">
      <div role="radiogroup" aria-label="Ambiente de IA" className={screen.stack}>
        {AI_ENVIRONMENT_OPTIONS.map((option) => {
          const checked = env === option.id;
          const detected = option.supported && discovery.aiEnvironments.some((e) => e.id === option.id);

          return (
            <label key={option.id} className={screen.choice} data-checked={checked} aria-disabled={!option.supported}>
              <input
                type="radio"
                name="ambiente"
                value={option.id}
                checked={checked}
                disabled={!option.supported || disabled}
                onChange={() => onEnv(option.id)}
              />
              <span className={screen.choiceBody}>
                <span className={screen.rowTitle}>{option.name}</span>
                {!option.supported ? <span className={screen.rowMeta}> — sem suporte nesta fase</span> : null}
              </span>
              {detected ? <Tag tone="accent">detectado</Tag> : null}
              <span className={screen.radio} aria-hidden="true" />
            </label>
          );
        })}
      </div>
    </Section>
  );
}
