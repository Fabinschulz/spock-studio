import { EvidenceDisclosure, InlineNotice, StatusChip } from '@/shared/components/ui';
import type { PlannedCommand } from '@/shared/schema';

import { ChoiceField } from '../../components/choice-field';
import styles from '../../components/panels.module.scss';
import { effectLabel } from '../../domain/labels';

interface CommandCardProps {
  command: PlannedCommand;
  approved: boolean;
  onToggle: () => void;
}

export function CommandCard({ command, approved, onToggle }: CommandCardProps) {
  return (
    <div className={styles.card}>
      <div className={styles.cardTitle}>{command.description}</div>
      <code className={styles.mono}>
        {command.executable} {command.arguments.join(' ')}
      </code>
      <div className={styles.chips}>
        {command.effects.map((effect) => (
          <StatusChip key={effect} tone={effect === 'network' ? 'warning' : 'info'}>
            {effectLabel[effect]}
          </StatusChip>
        ))}
      </div>
      <EvidenceDisclosure summary="Pastas afetadas" items={command.affectedPaths} />
      {command.destructive ? (
        <InlineNotice tone="warning">
          Este comando sobrescreve edições locais da estrutura de especificação. Os arquivos atuais são copiados antes e
          podem ser restaurados.
        </InlineNotice>
      ) : null}
      <ChoiceField type="checkbox" checked={approved} onChange={onToggle}>
        <span>
          Aprovar a execução deste comando
          <br />
          <span className={styles.muted}>
            Desmarcado por padrão: executar programas e acessar a rede exige a sua aprovação.
          </span>
        </span>
      </ChoiceField>
    </div>
  );
}
