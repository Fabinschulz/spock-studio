import { useProjectSelectionContext } from '@/features/project-selection';
import { InlineNotice, Radio } from '@/shared/components';
import { handoffState } from '@/shared/routing';
import { ArrowRightIcon, Button, TextField } from '@mariliz/ui-kit';
import { useId, useState, type FormEvent, type KeyboardEvent } from 'react';
import { Link, useNavigate } from 'react-router';

import { ASK_KINDS, ASK_OPTIONS, askNotice, suggestAskKind, type AskKind, type AskReadiness } from '../domain/intent';
import styles from './request-composer.module.scss';

const MIN_TEXT = 3;
const MAX_SHORTCUTS = 3;

/** Uma tela que combina com o que a pessoa escreveu: às vezes ela procurava um lugar, não queria abrir um pedido. */
export interface ComposerShortcut {
  id: string;
  label: string;
  hint: string;
  to: string;
}

/** O que a pessoa pediu, já com o projeto: é o que a camada de cima entrega ao Boot. */
export interface ComposerRequest {
  projectId: string;
  text: string;
  kind: AskKind;
  /** A pessoa marcou o tipo de propósito; sem isso, o tipo é só a sugestão feita pelo texto. */
  explicit: boolean;
}

interface RequestComposerProps {
  findShortcuts?: (text: string) => readonly ComposerShortcut[];
  /**
   * Quem prepara o pedido antes de a pessoa chegar à tela (o Boot). Devolve verdadeiro quando assumiu: o composer então não
   * navega. Falso ou ausente: o caminho de sempre, direto para a tela do tipo escolhido.
   */
  onRequest?: (request: ComposerRequest) => Promise<boolean>;
  /** O que o motor disse estar pronto. Sem isso, a entrada não avisa nada. */
  readiness?: AskReadiness;
}

/**
 * A pessoa escreve o que quer e escolhe (ou aceita) o caminho. Nada é executado aqui: o pedido vai para o Boot, que o
 * interpreta e mostra o plano; sem Boot, o texto segue para a tela que trata aquele tipo de pedido.
 */
export function RequestComposer({ findShortcuts, onRequest, readiness }: RequestComposerProps) {
  const hintId = useId();
  const noticeId = useId();
  const navigate = useNavigate();
  const { selected, pick, busy, unavailableReason } = useProjectSelectionContext();
  const [text, setText] = useState('');
  // Enquanto a pessoa não escolhe um tipo, vale o sugerido pelo texto; depois da escolha, a dela fica.
  const [chosen, setChosen] = useState<AskKind | null>(null);

  const trimmed = text.trim();
  const kind = chosen ?? suggestAskKind(trimmed);
  const option = ASK_OPTIONS[kind];
  const notice = readiness ? askNotice(kind, readiness) : null;
  const ready = trimmed.length >= MIN_TEXT && !busy && !unavailableReason;
  const shortcuts = trimmed.length >= MIN_TEXT ? (findShortcuts?.(trimmed).slice(0, MAX_SHORTCUTS) ?? []) : [];

  const submit = async (event?: FormEvent) => {
    event?.preventDefault();
    if (!ready) return;
    const project = selected ?? (await pick());
    if (!project) return;
    if (
      onRequest &&
      (await onRequest({ projectId: project.projectId, text: trimmed, kind, explicit: chosen !== null }))
    )
      return;
    navigate(option.to, { state: handoffState(trimmed) });
  };

  const onKeyDown = (event: KeyboardEvent) => {
    if (event.key !== 'Enter' || event.shiftKey || event.nativeEvent.isComposing) return;
    event.preventDefault();
    void submit();
  };

  return (
    <form className={styles.composer} aria-label="Pedido" onSubmit={(event) => void submit(event)}>
      <TextField
        label="O que você deseja fazer?"
        placeholder="Ex.: verifique o impacto de trocar o contrato de autenticação"
        multiline
        minRows={2}
        maxRows={8}
        fullWidth
        value={text}
        onChange={(event) => setText(event.target.value)}
        onKeyDown={onKeyDown}
        slotProps={{ htmlInput: { 'aria-describedby': notice ? `${hintId} ${noticeId}` : hintId } }}
      />

      <fieldset className={styles.kinds}>
        <legend className={styles.legend}>Tipo de atividade</legend>
        {ASK_KINDS.map((entry) => (
          <Radio
            key={entry}
            name="tipo-de-atividade"
            value={entry}
            label={ASK_OPTIONS[entry].label}
            checked={entry === kind}
            onChange={() => setChosen(entry)}
          />
        ))}
      </fieldset>

      {notice ? (
        <div id={noticeId}>
          <InlineNotice tone="warning">
            {notice.text}
            {notice.action ? (
              <>
                {' '}
                <Link to={notice.action.to}>{notice.action.label}</Link>
              </>
            ) : null}
          </InlineNotice>
        </div>
      ) : null}

      <div className={styles.footer}>
        <p id={hintId} className={styles.hint} aria-live="polite">
          {option.description}{' '}
          {unavailableReason ??
            (selected ? `Projeto: ${selected.displayName}.` : 'Ao continuar, você escolhe a pasta do projeto.')}{' '}
          Enter continua; Shift+Enter quebra a linha.
        </p>
        <Button type="submit" variant="contained" disabled={!ready} endIcon={<ArrowRightIcon fontSize="small" />}>
          Continuar
        </Button>
      </div>

      {shortcuts.length > 0 ? (
        <nav aria-label="Telas relacionadas ao que você escreveu" className={styles.shortcuts}>
          <span className={styles.shortcutsLabel}>Ou vá direto para</span>
          <ul className={styles.shortcutList} role="list">
            {shortcuts.map((shortcut) => (
              <li key={shortcut.id}>
                <Link to={shortcut.to} className={styles.shortcut}>
                  {shortcut.label}
                  <span className={styles.shortcutHint}> · {shortcut.hint}</span>
                </Link>
              </li>
            ))}
          </ul>
        </nav>
      ) : null}
    </form>
  );
}
