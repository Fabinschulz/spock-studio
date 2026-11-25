import { Checkbox } from '@mariliz/ui-kit';

import { EvidenceDisclosure, InlineNotice } from '@/shared/components/ui';
import type { PlannedCommand } from '@/shared/schema';

import rows from '../../components/rows.module.scss';
import { isSpecificationCommand } from '../../domain/approvals';
import { effectLabel } from '../../domain/labels';

interface CommandRowProps {
  command: PlannedCommand;
  approved: boolean;
  onToggle: () => void;
}

/** Desmarcado por padrão: o aviso fica na seção. */
export function CommandRow({ command, approved, onToggle }: CommandRowProps) {
  return (
    <li>
      <div className={rows.line}>
        <Checkbox checked={approved} onChange={onToggle} label={`Aprovar a execução: ${command.description}`} />
        <span className={rows.end}>{command.effects.map((effect) => effectLabel[effect]).join(' · ')}</span>
      </div>
      <code className={rows.mono}>
        {command.executable} {command.arguments.join(' ')}
      </code>
      {isSpecificationCommand(command.id) ? (
        <p className={rows.meta}>
          Sem aprovar, a estrutura de especificação não é instalada e as verificações que dependem dela ficam bloqueadas
          no Resultado.
        </p>
      ) : null}
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
