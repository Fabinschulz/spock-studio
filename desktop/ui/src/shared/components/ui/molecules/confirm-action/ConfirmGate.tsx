import { useState, type ReactNode } from 'react';
import { ConfirmAction } from './ConfirmAction';

interface ConfirmGateProps {
  label: string;
  confirmLabel: string;
  cancelLabel?: string;
  busy?: boolean;
  /** O texto da confirmação: o que acontece se a pessoa seguir. */
  message: ReactNode;
  onConfirm: () => void;
  /** Os botões de antes da confirmação; `ask` os troca por ela. */
  children: (ask: () => void) => ReactNode;
}

/**
 * A alternância entre os botões e a confirmação de uma ação que não se desfaz com um clique. A confirmação fecha antes de
 * a ação começar: o `busy` da ação aparece nos botões que voltam, e um erro não deixa a confirmação presa na tela.
 */
export function ConfirmGate({ message, onConfirm, children, ...confirm }: ConfirmGateProps) {
  const [asking, setAsking] = useState(false);
  if (!asking) return <>{children(() => setAsking(true))}</>;
  return (
    <ConfirmAction
      {...confirm}
      onConfirm={() => {
        setAsking(false);
        onConfirm();
      }}
      onCancel={() => setAsking(false)}
    >
      {message}
    </ConfirmAction>
  );
}
