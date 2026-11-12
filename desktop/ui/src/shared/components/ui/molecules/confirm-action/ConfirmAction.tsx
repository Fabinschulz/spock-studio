import { Button } from '@mariliz/ui-kit';
import { useEffect, useRef, type ReactNode } from 'react';
import styles from './ConfirmAction.module.scss';

interface ConfirmActionProps {
  /** O nome do grupo: o que está sendo confirmado ("Remover GitHub da equipe"). */
  label: string;
  /** O que acontece e, quando ajuda, o que não muda. É a frase que a pessoa lê antes de decidir. */
  children: ReactNode;
  confirmLabel: string;
  /** O texto de desistir. Padrão: "Manter". */
  cancelLabel?: string;
  busy?: boolean;
  onConfirm: () => void;
  onCancel: () => void;
}

/**
 * A confirmação de uma ação que apaga algo, feita na própria linha: diz o que vai acontecer e pede o "sim" com um botão
 * de perigo, ao lado da opção de desistir.
 *
 * O foco vai para a opção segura ao aparecer, porque o botão que abriu a confirmação sai da tela e o foco cairia no vazio;
 * e o Esc desiste. Quem usa o teclado confirma só de propósito: Enter na primeira opção focada nunca apaga nada.
 */
export function ConfirmAction({
  label,
  children,
  confirmLabel,
  cancelLabel = 'Manter',
  busy = false,
  onConfirm,
  onCancel
}: ConfirmActionProps) {
  const group = useRef<HTMLDivElement>(null);
  const cancel = useRef<HTMLButtonElement>(null);

  useEffect(() => {
    cancel.current?.focus();
  }, []);

  // O ouvinte vai no elemento, e não como `onKeyDown` do <div>, que não é um controle: o Esc sobe dos botões de dentro.
  useEffect(() => {
    const element = group.current;
    if (!element) return;
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') onCancel();
    };
    element.addEventListener('keydown', onKeyDown);
    return () => element.removeEventListener('keydown', onKeyDown);
  }, [onCancel]);

  return (
    <div ref={group} className={styles.confirm} role="group" aria-label={label}>
      <p className={styles.message}>{children}</p>
      <div className={styles.actions}>
        <Button type="button" variant="contained" color="error" disabled={busy} onClick={onConfirm}>
          {confirmLabel}
        </Button>
        <Button ref={cancel} type="button" variant="outlined" disabled={busy} onClick={onCancel}>
          {cancelLabel}
        </Button>
      </div>
    </div>
  );
}
