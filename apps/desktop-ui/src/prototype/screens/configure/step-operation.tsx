import type { OperationView } from '../../domain/operation-model';
import type { Replay } from '../../hooks/use-replay';
import screen from '../screen.module.scss';

import { OperationTimeline } from './operation-timeline';
import { ReplayBar, type Contract } from './replay-bar';
import { StepHeader } from './step-project';

interface Props {
  view: OperationView;
  replay: Replay;
  contract: Contract;
  onContract: (contract: Contract) => void;
}

export function OperationStep({ view, replay, contract, onContract }: Props) {
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
