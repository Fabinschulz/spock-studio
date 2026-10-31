import { OperationTimeline } from '@/prototype/components/operation-timeline';
import { ReplayBar } from '@/prototype/components/replay-bar';
import { StepHeader } from '@/prototype/components/step-header';
import screen from '@/prototype/styles/screen.module.scss';

import type { OperationStepProps } from './types';

export function OperationStep({ view, replay, contract, onContract }: OperationStepProps) {
  return (
    <>
      <StepHeader
        title="Aplicando a configuração"
        lead="O motor executa só o que você aprovou, na ordem abaixo. Se algo falhar ou você cancelar, o que já foi feito é desfeito."
      />
      <ReplayBar replay={replay} contract={contract} onContract={onContract} />
      <OperationTimeline view={view} clock={replay.clock} proposed={contract === 'proposed'} />
      {contract === 'proposed' ? (
        <p className={screen.note}>
          No contrato proposto o motor declara as etapas no início (run.plan) e publica cada Eval (eval.started e
          eval.completed). Hoje esses eventos não existem.
        </p>
      ) : null}
    </>
  );
}
