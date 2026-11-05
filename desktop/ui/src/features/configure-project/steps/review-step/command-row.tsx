import { EvidenceDisclosure, InlineNotice } from '@/shared/components/ui';
import type { PlannedCommand } from '@/shared/schema';

import { CheckboxField } from '../../components/choice-field';
import { effectLabel } from '../../domain/labels';
import styles from './review.module.scss';

interface CommandRowProps {
  command: PlannedCommand;
  approved: boolean;
  onToggle: () => void;
}

/** Uma linha por comando: o que executa, o que toca e a aprovação. Desmarcado por padrão (o aviso fica na seção). */
export function CommandRow({ command, approved, onToggle }: CommandRowProps) {
  return (
    <li>
      <div className={styles.line}>
        <CheckboxField
          plain
          checked={approved}
          onChange={onToggle}
          label={`Aprovar a execução: ${command.description}`}
        />
        <span className={styles.end}>{command.effects.map((effect) => effectLabel[effect]).join(' · ')}</span>
      </div>
      <code className={styles.mono}>
        {command.executable} {command.arguments.join(' ')}
      </code>
      <EvidenceDisclosure summary="Pastas afetadas" items={command.affectedPaths} />
      {command.destructive ? (
        <InlineNotice tone="warning">
          Este comando sobrescreve edições locais da estrutura de especificação. Os arquivos atuais são copiados antes e
          podem ser restaurados.
        </InlineNotice>
      ) : null}
    </li>
  );
}
