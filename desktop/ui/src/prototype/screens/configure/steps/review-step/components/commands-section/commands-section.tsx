import { ApproveCheckbox } from '@/prototype/components/approve-checkbox';
import { Section } from '@/prototype/components/section';
import { COMMAND_EFFECT_TEXT } from '@/prototype/constants/review';
import screen from '@/prototype/styles/screen.module.scss';

import type { CommandsSectionProps } from './types';

export function CommandsSection({ commands, approved, onToggle }: CommandsSectionProps) {
  return (
    <Section title="Comandos que executam fora dos arquivos" id="comandos" aside="exigem sua aprovação">
      <ul role="list" className={screen.rows}>
        {commands.map((command) => (
          <li key={command.id} className={screen.row}>
            <ApproveCheckbox
              checked={approved.includes(command.id)}
              label={`Aprovar: ${command.description}`}
              onChange={() => onToggle(command.id)}
            />
            <span>
              <span className={screen.rowTitle}>{command.description}</span>
              <span className={`${screen.mono} ${screen.block}`}>
                {command.executable} {command.arguments.join(' ')}
              </span>
              <span className={screen.rowMeta}>{command.effects.map((e) => COMMAND_EFFECT_TEXT[e]).join(' · ')}</span>
            </span>
            <span />
          </li>
        ))}
      </ul>
    </Section>
  );
}
