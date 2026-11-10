import { Button } from '@mariliz/ui-kit';

/** O texto diz o que a aprovação vai fazer: executar o que foi marcado, ou só seguir sem gravar nada. */
export function ApproveButton({
  busy,
  hasActions,
  chosenCount,
  onApprove
}: {
  busy: boolean;
  hasActions: boolean;
  chosenCount: number;
  onApprove: () => void;
}) {
  return (
    <Button type="button" onClick={onApprove} disabled={busy}>
      {label(busy, hasActions, chosenCount)}
    </Button>
  );
}

function label(busy: boolean, hasActions: boolean, chosenCount: number): string {
  if (busy) return 'Retomando…';
  if (!hasActions) return 'Aprovar';
  return chosenCount > 0 ? 'Aprovar e executar as ações marcadas' : 'Aprovar sem executar ações';
}
