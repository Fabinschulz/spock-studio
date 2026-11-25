import { Button, TabBar } from '@mariliz/ui-kit';
import { useState } from 'react';

import { ConfirmAction, PrimaryActionFooter } from '@/shared/components/ui';
import type { ConfigurationPlan, ConflictResolution, FileChange } from '@/shared/schema';
import { plural } from '@/shared/utils';

import panels from '../../components/panels.module.scss';
import rows from '../../components/rows.module.scss';
import { StepHeader } from '../../components/step-header';
import { ChangesTab } from './changes-tab';
import { DecisionsList } from './decisions-list';
import styles from './review.module.scss';
import { RisksTab, risksCount } from './risks-tab';

interface ReviewStepProps {
  plan: ConfigurationPlan;
  approvedChanges: readonly string[];
  approvedCommands: readonly string[];
  resolutions: Readonly<Record<string, ConflictResolution>>;
  approvalCount: number;
  onToggleChange: (id: string) => void;
  onSetChanges?: (ids: readonly string[], approved: boolean) => void;
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

/** Plano e revisão numa etapa só: o que muda (e se aprova), por que muda e o que fica de risco. */
export function ReviewStep({
  plan,
  approvedChanges,
  approvedCommands,
  resolutions,
  approvalCount,
  onToggleChange,
  onSetChanges = () => undefined,
  onToggleCommand,
  onResolution,
  onBack,
  onCancel,
  onApply
}: ReviewStepProps) {
  const [discarding, setDiscarding] = useState(false);
  const blocked = plan.decisions.filter((decision) => decision.decision === 'blocked').length;
  const risks = risksCount(plan);
  const fromEngineer = plan.proposalOrigin === 'harness-engineer';

  return (
    <section className={panels.step}>
      <StepHeader title="Revisão">
        Confira o que muda, por que muda e o que fica de risco, e aprove o que quiser aplicar. O que você não aprovar
        não é tocado.
      </StepHeader>

      <p className={rows.note} aria-label="Resumo do plano">
        {changesSummary(plan.changes)}
        {plan.commands.length > 0 ? ` · ${plural(plan.commands.length, 'comando', 'comandos')}` : ''}
      </p>

      <TabBar
        aria-label="Revisão do plano"
        tabs={[
          { label: 'Alterações' },
          { label: blocked > 0 ? `Por quê (${plural(blocked, 'bloqueada', 'bloqueadas')})` : 'Por quê' },
          { label: risks > 0 ? `Riscos (${risks})` : 'Riscos' }
        ]}
      >
        <div className={panels.stack}>
          <ChangesTab
            plan={plan}
            approvedChanges={approvedChanges}
            approvedCommands={approvedCommands}
            resolutions={resolutions}
            onToggleChange={onToggleChange}
            onSetChanges={onSetChanges}
            onToggleCommand={onToggleCommand}
            onResolution={onResolution}
          />
        </div>
        <div className={panels.stack}>
          <p className={styles.origin}>
            {fromEngineer
              ? 'Origem do plano: a proposta do Harness Engineer, validada pelo motor (formato, catálogo de componentes e ausência de segredos).'
              : 'Origem do plano: as regras-base do Spock.'}
          </p>
          <DecisionsList decisions={plan.decisions} />
        </div>
        <div className={panels.stack}>
          <RisksTab plan={plan} />
        </div>
      </TabBar>

      {discarding ? (
        <ConfirmAction
          label="Descartar o plano"
          confirmLabel="Descartar o plano"
          cancelLabel="Continuar revisando"
          onConfirm={onCancel}
          onCancel={() => setDiscarding(false)}
        >
          O plano, as aprovações e as escolhas de conflito desta revisão serão descartados. Nada foi gravado no projeto.
        </ConfirmAction>
      ) : null}

      <PrimaryActionFooter>
        <Button variant="outlined" color="inherit" onClick={onBack}>
          Voltar
        </Button>
        <Button variant="outlined" color="inherit" onClick={() => setDiscarding(true)}>
          Cancelar sem aplicar
        </Button>
        <Button variant="contained" onClick={onApply} disabled={approvalCount === 0}>
          {applyLabel(approvalCount)}
        </Button>
      </PrimaryActionFooter>
    </section>
  );
}
