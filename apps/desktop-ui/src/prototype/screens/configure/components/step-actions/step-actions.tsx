import { Button } from '@synthra.io/ui-kit';

import type { StepActionsProps } from './types';

export function StepActions({
  slug,
  state,
  actions,
  approvedCount,
  operationFinished,
  go,
  onConclude
}: StepActionsProps) {
  switch (slug) {
    case 'projeto':
      return (
        <Button variant="contained" disabled={state.analysis !== 'ready'} onClick={() => go('modelo')}>
          Continuar
        </Button>
      );
    case 'modelo':
      return (
        <>
          <Button variant="outlined" color="inherit" disabled={state.planning} onClick={() => go('projeto')}>
            Voltar
          </Button>
          <Button variant="contained" disabled={!state.env || state.planning} onClick={actions.generatePlan}>
            {state.planning ? 'Gerando plano…' : 'Gerar plano'}
          </Button>
        </>
      );
    case 'plano':
      return (
        <>
          <Button variant="outlined" color="inherit" onClick={() => go('modelo')}>
            Voltar
          </Button>
          <Button variant="contained" onClick={() => go('revisao')}>
            Revisar alterações
          </Button>
        </>
      );
    case 'revisao':
      return (
        <>
          <Button variant="outlined" color="inherit" onClick={() => go('plano')}>
            Voltar
          </Button>
          <Button variant="contained" disabled={approvedCount === 0} onClick={() => go('aplicacao')}>
            Aplicar configuração ({approvedCount} aprovados)
          </Button>
        </>
      );
    case 'aplicacao':
      return (
        <>
          <Button variant="outlined" color="inherit" disabled={operationFinished} onClick={() => go('revisao')}>
            Cancelar e desfazer
          </Button>
          <Button variant="contained" disabled={!operationFinished} onClick={() => go('resultado')}>
            Ver resultado
          </Button>
        </>
      );
    case 'resultado':
      return (
        <>
          <Button variant="outlined" color="inherit" onClick={() => undefined}>
            Reverter alterações
          </Button>
          <Button variant="outlined" color="inherit" onClick={() => go('aplicacao')}>
            Rever execução
          </Button>
          <Button variant="contained" onClick={onConclude}>
            Concluir
          </Button>
        </>
      );
  }
}
