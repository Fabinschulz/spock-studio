import { Checkbox } from '@mariliz/ui-kit';

import { EvidenceDisclosure } from '@/shared/components/ui';
import type { ConfigurationPlan, ConflictResolution, FileChange } from '@/shared/schema';
import { plural } from '@/shared/utils';

import rows from '../../components/rows.module.scss';
import { componentKindGroupLabel } from '../../domain/labels';
import { CommandRow } from './command-row';
import { FileChangeRow } from './file-change-row';
import styles from './review.module.scss';

interface ChangesTabProps {
  plan: ConfigurationPlan;
  approvedChanges: readonly string[];
  approvedCommands: readonly string[];
  resolutions: Readonly<Record<string, ConflictResolution>>;
  onToggleChange: (id: string) => void;
  onSetChanges: (ids: readonly string[], approved: boolean) => void;
  onToggleCommand: (id: string) => void;
  onResolution: (id: string, value: ConflictResolution) => void;
}

/** Agrupa por tipo de componente, na ordem em que o plano os traz. */
function groupByKind(changes: readonly FileChange[]): [FileChange['kind'], FileChange[]][] {
  const groups = new Map<FileChange['kind'], FileChange[]>();
  for (const change of changes) groups.set(change.kind, [...(groups.get(change.kind) ?? []), change]);
  return [...groups];
}

export function ChangesTab({
  plan,
  approvedChanges,
  approvedCommands,
  resolutions,
  onToggleChange,
  onSetChanges,
  onToggleCommand,
  onResolution
}: ChangesTabProps) {
  const actionable = plan.changes.filter((change) => change.action !== 'preserve');
  const preserved = plan.changes.filter((change) => change.action === 'preserve');

  return (
    <>
      {plan.commands.length > 0 ? (
        <section aria-labelledby="comandos-titulo" className={rows.section}>
          <h3 id="comandos-titulo" className={rows.title}>
            Comandos que executam fora do arquivo
          </h3>
          <p className={rows.note}>
            Desmarcados por padrão: executar programas e acessar a rede exige a sua aprovação.
          </p>
          <ul role="list" className={rows.rows}>
            {plan.commands.map((command) => (
              <CommandRow
                key={command.id}
                command={command}
                approved={approvedCommands.includes(command.id)}
                onToggle={() => onToggleCommand(command.id)}
              />
            ))}
          </ul>
        </section>
      ) : null}

      <section aria-labelledby="arquivos-titulo" className={rows.section}>
        <h3 id="arquivos-titulo" className={rows.title}>
          Arquivos
        </h3>
        {actionable.length === 0 ? <p className={rows.note}>Nenhum arquivo precisa mudar.</p> : null}
        {groupByKind(actionable).map(([kind, changes]) => (
          <FileGroup
            key={kind}
            title={componentKindGroupLabel[kind]}
            changes={changes}
            approvedChanges={approvedChanges}
            resolutions={resolutions}
            onToggleChange={onToggleChange}
            onSetChanges={onSetChanges}
            onResolution={onResolution}
          />
        ))}
        {preserved.length > 0 ? (
          <EvidenceDisclosure
            summary={`${plural(preserved.length, 'arquivo preservado', 'arquivos preservados')}, sem alteração`}
            items={preserved.map((change) => change.path)}
          />
        ) : null}
      </section>

      {plan.validations.length > 0 ? (
        <section aria-labelledby="validacao-titulo" className={rows.section}>
          <h3 id="validacao-titulo" className={rows.title}>
            Depois de aplicar, a validação executa
          </h3>
          <ul role="list" className={styles.list}>
            {plan.validations.map((validation) => (
              <li key={validation}>{validation}</li>
            ))}
          </ul>
        </section>
      ) : null}
    </>
  );
}

interface FileGroupProps {
  title: string;
  changes: readonly FileChange[];
  approvedChanges: readonly string[];
  resolutions: Readonly<Record<string, ConflictResolution>>;
  onToggleChange: (id: string) => void;
  onSetChanges: (ids: readonly string[], approved: boolean) => void;
  onResolution: (id: string, value: ConflictResolution) => void;
}

/** Aprovar tudo ou nada por grupo. Conflito fica de fora: a decisão dele é manter ou substituir, arquivo a arquivo. */
function FileGroup({
  title,
  changes,
  approvedChanges,
  resolutions,
  onToggleChange,
  onSetChanges,
  onResolution
}: FileGroupProps) {
  const approvable = changes.filter((change) => change.action !== 'conflict').map((change) => change.id);
  const approved = approvable.filter((id) => approvedChanges.includes(id)).length;
  const label = `${title} (${changes.length})`;

  return (
    <div className={styles.group}>
      {approvable.length > 0 ? (
        <Checkbox
          label={label}
          checked={approved === approvable.length}
          indeterminate={approved > 0 && approved < approvable.length}
          onChange={(_, value) => onSetChanges(approvable, value)}
          labelProps={{ className: styles.groupLabel }}
        />
      ) : (
        <p className={styles.groupLabel}>{label}</p>
      )}
      <ul role="list" className={rows.rows} aria-label={title}>
        {changes.map((change) => (
          <FileChangeRow
            key={change.id}
            change={change}
            approved={approvedChanges.includes(change.id)}
            resolution={resolutions[change.id] ?? 'keep'}
            onToggle={() => onToggleChange(change.id)}
            onResolve={(value) => onResolution(change.id, value)}
          />
        ))}
      </ul>
    </div>
  );
}
