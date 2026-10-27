import { Alert, Button } from '@synthra.io/ui-kit';
import { useState } from 'react';

import { DiffView, StatusChip } from '@/shared/components/ui';
import type { ConfigurationPlan, FileChange } from '@/shared/schemas';

import { actionLabel, actionTone, effectLabel, kindLabel, originLabel } from '../../core/labels';
import { EvidenceList } from '../../ui/evidence-list';
import styles from '../../ui/panels.module.scss';
import { StepHeader } from '../../ui/step-header';

interface ReviewStepProps {
  plan: ConfigurationPlan;
  approvedChanges: readonly string[];
  approvedCommands: readonly string[];
  resolutions: Readonly<Record<string, 'keep' | 'overwrite'>>;
  approvalCount: number;
  onToggleChange: (id: string) => void;
  onToggleCommand: (id: string) => void;
  onResolution: (id: string, value: 'keep' | 'overwrite') => void;
  onBack: () => void;
  onCancel: () => void;
  onApply: () => void;
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
  const [expanded, setExpanded] = useState<ReadonlySet<string>>(new Set());
  const toggleDiff = (id: string) =>
    setExpanded((current) => {
      const next = new Set(current);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });

  const actionable = plan.changes.filter((c) => c.action !== 'preserve');
  const preserved = plan.changes.filter((c) => c.action === 'preserve');
  const count = (action: FileChange['action']) => plan.changes.filter((c) => c.action === action).length;

  return (
    <section className={styles.step}>
      <StepHeader title="Revisão das alterações">
        Confira o que será criado, atualizado ou removido, e aprove o que quiser aplicar. O que você não aprovar não é
        tocado.
      </StepHeader>

      <div className={styles.chips} aria-label="Resumo do plano">
        <StatusChip tone="success">{count('create')} a criar</StatusChip>
        <StatusChip tone="info">{count('update')} a atualizar</StatusChip>
        <StatusChip tone="warning">{count('remove')} a remover</StatusChip>
        <StatusChip tone={count('conflict') > 0 ? 'danger' : 'neutral'}>{count('conflict')} conflito(s)</StatusChip>
        <StatusChip>{count('preserve')} preservado(s)</StatusChip>
      </div>

      {plan.commands.length > 0 ? (
        <>
          <h3 className={styles.sectionTitle}>Comandos que executam fora do arquivo</h3>
          {plan.commands.map((cmd) => (
            <div key={cmd.id} className={styles.card}>
              <div className={styles.cardTitle}>{cmd.description}</div>
              <code className={styles.mono}>
                {cmd.executable} {cmd.arguments.join(' ')}
              </code>
              <div className={styles.chips}>
                {cmd.effects.map((e) => (
                  <StatusChip key={e} tone={e === 'network' ? 'warning' : 'info'}>
                    {effectLabel[e]}
                  </StatusChip>
                ))}
              </div>
              <EvidenceList summary="Pastas afetadas" items={cmd.affectedPaths} />
              {cmd.destructive ? (
                <Alert
                  severity="warning"
                  description="Este comando sobrescreve edições locais da skill do TLC. Os arquivos atuais são copiados antes e podem ser restaurados."
                />
              ) : null}
              <label className={styles.field}>
                <input
                  type="checkbox"
                  checked={approvedCommands.includes(cmd.id)}
                  onChange={() => onToggleCommand(cmd.id)}
                />
                <span>
                  Aprovar a execução deste comando
                  <br />
                  <span className={styles.muted}>
                    Desmarcado por padrão: executar programas e acessar a rede exige a sua aprovação.
                  </span>
                </span>
              </label>
            </div>
          ))}
        </>
      ) : null}

      <h3 className={styles.sectionTitle}>Arquivos</h3>
      {actionable.length === 0 ? <p className={styles.muted}>Nenhum arquivo precisa mudar.</p> : null}
      <ul className={styles.list} aria-label="Alterações em arquivos">
        {actionable.map((change) => (
          <li key={change.id} className={styles.card}>
            <div className={styles.cardTitle}>
              <StatusChip tone={actionTone(change.action)}>{actionLabel[change.action]}</StatusChip>
              <span className={styles.mono}>{change.path}</span>
              {change.destructive && change.action !== 'conflict' ? (
                <StatusChip tone="danger">destrutivo</StatusChip>
              ) : null}
            </div>
            <span className={styles.muted}>
              {kindLabel[change.kind]} · {originLabel[change.origin] ?? change.origin}
            </span>
            <span>{change.reason}</span>

            {change.action === 'conflict' ? (
              <>
                <Alert
                  severity="warning"
                  description={change.conflictReason ?? 'O arquivo atual é diferente do proposto.'}
                />
                <fieldset className={styles.fieldset}>
                  <legend>{`Decisão para ${change.path}`}</legend>
                  <label className={styles.field}>
                    <input
                      type="radio"
                      name={`resolution-${change.id}`}
                      checked={(resolutions[change.id] ?? 'keep') === 'keep'}
                      onChange={() => onResolution(change.id, 'keep')}
                    />
                    <span>Manter o meu arquivo (padrão)</span>
                  </label>
                  <label className={styles.field}>
                    <input
                      type="radio"
                      name={`resolution-${change.id}`}
                      checked={resolutions[change.id] === 'overwrite'}
                      onChange={() => onResolution(change.id, 'overwrite')}
                    />
                    <span>Substituir pelo proposto (o meu fica guardado em backup e pode ser restaurado)</span>
                  </label>
                </fieldset>
              </>
            ) : (
              <label className={styles.field}>
                <input
                  type="checkbox"
                  checked={approvedChanges.includes(change.id)}
                  onChange={() => onToggleChange(change.id)}
                />
                <span>{`Aprovar ${actionLabel[change.action].toLowerCase()} ${change.path}`}</span>
              </label>
            )}

            {change.diff ? (
              <>
                <Button variant="text" onClick={() => toggleDiff(change.id)} aria-expanded={expanded.has(change.id)}>
                  {expanded.has(change.id) ? 'Ocultar diferenças' : 'Ver diferenças'}
                </Button>
                {expanded.has(change.id) ? <DiffView diff={change.diff} label={change.path} /> : null}
              </>
            ) : null}
          </li>
        ))}
      </ul>

      {preserved.length > 0 ? (
        <EvidenceList
          summary={`${preserved.length} arquivo(s) preservado(s), sem alteração`}
          items={preserved.map((c) => c.path)}
        />
      ) : null}

      {plan.validations.length > 0 ? (
        <div className={styles.card}>
          <h3 className={styles.cardTitle}>Depois de aplicar, a validação executa</h3>
          <ul className={styles.list}>
            {plan.validations.map((v) => (
              <li key={v}>{v}</li>
            ))}
          </ul>
        </div>
      ) : null}

      <div className={styles.actions}>
        <Button variant="outlined" color="inherit" onClick={onBack}>
          Voltar
        </Button>
        <Button variant="outlined" color="inherit" onClick={onCancel}>
          Cancelar sem aplicar
        </Button>
        <Button variant="contained" onClick={onApply} disabled={approvalCount === 0}>
          {approvalCount === 0
            ? 'Nada aprovado para aplicar'
            : `Aplicar configuração (${approvalCount} aprovado${approvalCount === 1 ? '' : 's'})`}
        </Button>
      </div>
    </section>
  );
}
