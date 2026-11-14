import { useProjectSelectionContext } from '@/features/project-selection';
import { EmptyState, ErrorState } from '@/shared/components';
import { Button } from '@mariliz/ui-kit';
import { useEffect, useRef, useState } from 'react';
import '@xterm/xterm/css/xterm.css';

import { useTerminalSession, type SessionStatus } from '../hooks/use-terminal-session';
import { tauriTerminalApi, type TerminalApi } from '../service/terminal-api';
import styles from './TerminalPanel.module.scss';

interface TerminalPanelProps {
  hosted: boolean;
  /** Muda cada vez que a pessoa pede o terminal (atalho ou botão): só então ele toma o foco. */
  focusRequest: number;
  api?: TerminalApi;
}

/**
 * Trocar de projeto ou abrir de novo remonta o terminal: a sessão anterior é encerrada e outra nasce do zero. O foco só
 * vai para o terminal quando a pessoa o pede; o painel que reabre sozinho, ao iniciar o aplicativo, não toma o foco.
 */
export function TerminalPanel({ hosted, focusRequest, api = tauriTerminalApi }: TerminalPanelProps) {
  const { selected } = useProjectSelectionContext();
  const [attempt, setAttempt] = useState(0);
  const [seenRequest, setSeenRequest] = useState(focusRequest);
  const [wantsFocus, setWantsFocus] = useState(focusRequest > 0);
  if (focusRequest !== seenRequest) {
    setSeenRequest(focusRequest);
    setWantsFocus(true);
  }

  if (!hosted) return <EmptyState>O terminal roda só no aplicativo desktop.</EmptyState>;
  if (!selected?.recentId)
    return <EmptyState>Abra um projeto para usar o terminal: ele roda na pasta do projeto aberto.</EmptyState>;

  return (
    <ProjectTerminal
      key={`${selected.recentId}:${attempt}`}
      api={api}
      recentId={selected.recentId}
      projectName={selected.displayName}
      wantsFocus={wantsFocus}
      onFocused={() => setWantsFocus(false)}
      onReopen={() => {
        setAttempt((value) => value + 1);
        setWantsFocus(true);
      }}
    />
  );
}

function statusText(status: Exclude<SessionStatus, { kind: 'failed' }>, projectName: string): string {
  switch (status.kind) {
    case 'starting':
      return `Abrindo o terminal em ${projectName}…`;
    case 'running':
      return `${status.shell} em ${projectName}`;
    case 'exited':
      return status.code === null ? 'O terminal foi encerrado.' : `O terminal foi encerrado (código ${status.code}).`;
  }
}

interface ProjectTerminalProps {
  api: TerminalApi;
  recentId: string;
  projectName: string;
  wantsFocus: boolean;
  onFocused: () => void;
  onReopen: () => void;
}

function ProjectTerminal({
  api,
  recentId,
  projectName,
  wantsFocus,
  onFocused,
  onReopen: reopen
}: ProjectTerminalProps) {
  const container = useRef<HTMLDivElement>(null);
  const { status, focus } = useTerminalSession(api, recentId, container);

  // O shell leva alguns segundos para ficar pronto: o pedido de foco espera por ele.
  useEffect(() => {
    if (!wantsFocus || status.kind !== 'running') return;
    focus();
    onFocused();
  }, [wantsFocus, status.kind, focus, onFocused]);

  return (
    <div className={styles.terminal}>
      {status.kind === 'failed' ? (
        <ErrorState onRetry={reopen}>{status.message}</ErrorState>
      ) : (
        <div className={styles.bar}>
          <p role="status" className={styles.status}>
            {statusText(status, projectName)}
          </p>
          <p className={styles.note}>Variáveis de ambiente com segredo ficam de fora deste terminal.</p>
          {status.kind === 'exited' ? (
            <Button size="small" variant="outlined" color="inherit" onClick={reopen}>
              Abrir de novo
            </Button>
          ) : null}
        </div>
      )}
      <div
        ref={container}
        className={styles.screen}
        role="group"
        aria-label={`Terminal de ${projectName}`}
        hidden={status.kind === 'failed'}
      />
    </div>
  );
}
