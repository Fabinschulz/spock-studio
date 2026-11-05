import { Button } from '@mariliz/ui-kit';

import { EvidenceDisclosure, PrimaryActionFooter, StatusChip } from '@/shared/components/ui';
import type { ConfigurationPlan, ConflictResolution, FileChange } from '@/shared/schema';

import styles from '../../components/panels.module.scss';
import { StepHeader } from '../../components/step-header';
import { CommandCard } from './command-card';
import { FileChangeCard } from './file-change-card';

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
  const countOf = (action: FileChange['action']) => plan.changes.filter((change) => change.action === action).length;

  return (
    <section className={styles.step}>
      <StepHeader title="Revisão das alterações">
        Confira o que será criado, atualizado ou removido, e aprove o que quiser aplicar. O que você não aprovar não é
        tocado.
      </StepHeader>

      <div className={styles.chips} aria-label="Resumo do plano">
        <StatusChip tone="success">{countOf('create')} a criar</StatusChip>
        <StatusChip tone="info">{countOf('update')} a atualizar</StatusChip>
        <StatusChip tone="warning">{countOf('remove')} a remover</StatusChip>
        <StatusChip tone={countOf('conflict') > 0 ? 'danger' : 'neutral'}>{countOf('conflict')} conflito(s)</StatusChip>
        <StatusChip>{countOf('preserve')} preservado(s)</StatusChip>
      </div>

      {plan.commands.length > 0 ? (
        <>
          <h3 className={styles.sectionTitle}>Comandos que executam fora do arquivo</h3>
          {plan.commands.map((command) => (
            <CommandCard
              key={command.id}
              command={command}
              approved={approvedCommands.includes(command.id)}
              onToggle={() => onToggleCommand(command.id)}
            />
          ))}
        </>
      ) : null}

      <h3 className={styles.sectionTitle}>Arquivos</h3>
      {actionable.length === 0 ? <p className={styles.muted}>Nenhum arquivo precisa mudar.</p> : null}
      <ul className={styles.list} aria-label="Alterações em arquivos">
        {actionable.map((change) => (
          <FileChangeCard
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

      {plan.validations.length > 0 ? (
        <div className={styles.card}>
          <h3 className={styles.cardTitle}>Depois de aplicar, a validação executa</h3>
          <ul className={styles.list}>
            {plan.validations.map((validation) => (
              <li key={validation}>{validation}</li>
            ))}
          </ul>
        </div>
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
