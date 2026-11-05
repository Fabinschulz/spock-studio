import { Button } from '@mariliz/ui-kit';

import { EvidenceDisclosure, PrimaryActionFooter } from '@/shared/components/ui';
import type { ConfigurationPlan, ConflictResolution, FileChange } from '@/shared/schema';

import panels from '../../components/panels.module.scss';
import { StepHeader } from '../../components/step-header';
import { CommandRow } from './command-row';
import { FileChangeRow } from './file-change-row';
import styles from './review.module.scss';

interface ReviewStepProps {
  plan: ConfigurationPlan;
  approvedChanges: readonly string[];
  approvedCommands: readonly string[];
  resolutions: Readonly<Record<string, ConflictResolution>>;
  approvalCount: number;
  onToggleChange: (id: string) => void;
  onToggleCommand: (id: string) => void;
  onResolution: (id: string, value: ConflictResolution) => void;
  onBack: () => void;
  onCancel: () => void;
  onApply: () => void;
}

const applyLabel = (approvalCount: number) =>
  approvalCount === 0
    ? 'Nada aprovado para aplicar'
    : `Aplicar configuração (${approvalCount} aprovado${approvalCount === 1 ? '' : 's'})`;

/** "2 a criar · 1 a atualizar · 1 conflito": só o que existe, na ordem em que o plano é lido. */
export function changesSummary(changes: readonly FileChange[]): string {
  const count = (action: FileChange['action']) => changes.filter((change) => change.action === action).length;
  const conflicts = count('conflict');
  const parts = [
    [count('create'), 'a criar'],
    [count('update'), 'a atualizar'],
    [count('remove'), 'a remover'],
    [conflicts, conflicts === 1 ? 'conflito' : 'conflitos'],
    [count('preserve'), count('preserve') === 1 ? 'preservado' : 'preservados']
  ] as const;
  const text = parts.filter(([n]) => n > 0).map(([n, label]) => `${n} ${label}`);
  return text.length > 0 ? text.join(' · ') : 'Nenhuma alteração no plano';
}

export function ReviewStep({
  plan,
  approvedChanges,
  approvedCommands,
  resolutions,
  approvalCount,
  onToggleChange,
  onToggleCommand,
  onResolution,
  onBack,
  onCancel,
  onApply
}: ReviewStepProps) {
  const actionable = plan.changes.filter((change) => change.action !== 'preserve');
  const preserved = plan.changes.filter((change) => change.action === 'preserve');

  return (
    <section className={panels.step}>
      <StepHeader title="Revisão das alterações">
        Confira o que será criado, atualizado ou removido, e aprove o que quiser aplicar. O que você não aprovar não é
        tocado.
      </StepHeader>

      <p className={styles.note} aria-label="Resumo do plano">
        {changesSummary(plan.changes)}
      </p>

      {plan.commands.length > 0 ? (
        <section aria-labelledby="comandos-titulo" className={styles.section}>
          <h3 id="comandos-titulo" className={styles.title}>
            Comandos que executam fora do arquivo
          </h3>
          <p className={styles.note}>
            Desmarcados por padrão: executar programas e acessar a rede exige a sua aprovação.
          </p>
          <ul role="list" className={styles.rows}>
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

      <section aria-labelledby="arquivos-titulo" className={styles.section}>
        <h3 id="arquivos-titulo" className={styles.title}>
          Arquivos
        </h3>
        {actionable.length === 0 ? <p className={styles.note}>Nenhum arquivo precisa mudar.</p> : null}
        <ul role="list" className={styles.rows} aria-label="Alterações em arquivos">
          {actionable.map((change) => (
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
        {preserved.length > 0 ? (
          <EvidenceDisclosure
            summary={`${preserved.length} arquivo(s) preservado(s), sem alteração`}
            items={preserved.map((change) => change.path)}
          />
        ) : null}
      </section>

      {plan.validations.length > 0 ? (
        <section aria-labelledby="validacao-titulo" className={styles.section}>
          <h3 id="validacao-titulo" className={styles.title}>
            Depois de aplicar, a validação executa
          </h3>
          <ul role="list" className={styles.list}>
            {plan.validations.map((validation) => (
              <li key={validation}>{validation}</li>
            ))}
          </ul>
        </section>
      ) : null}

      <PrimaryActionFooter>
        <Button variant="outlined" color="inherit" onClick={onBack}>
          Voltar
        </Button>
        <Button variant="outlined" color="inherit" onClick={onCancel}>
          Cancelar sem aplicar
        </Button>
        <Button variant="contained" onClick={onApply} disabled={approvalCount === 0}>
          {applyLabel(approvalCount)}
        </Button>
      </PrimaryActionFooter>
    </section>
  );
}
