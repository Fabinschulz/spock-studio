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
  /** O painel está à vista nesta aba: o terminal recebe o foco. */
  active: boolean;
  api?: TerminalApi;
}

/** Trocar de projeto ou abrir de novo remonta o terminal: a sessão anterior é encerrada e outra nasce do zero. */
export function TerminalPanel({ hosted, active, api = tauriTerminalApi }: TerminalPanelProps) {
  const { selected } = useProjectSelectionContext();
  const [attempt, setAttempt] = useState(0);

  if (!hosted) return <EmptyState>O terminal roda só no aplicativo desktop.</EmptyState>;
  if (!selected?.recentId)
    return <EmptyState>Abra um projeto para usar o terminal: ele roda na pasta do projeto aberto.</EmptyState>;

  return (
    <ProjectTerminal
      key={`${selected.recentId}:${attempt}`}
      api={api}
      recentId={selected.recentId}
      projectName={selected.displayName}
      active={active}
      onReopen={() => setAttempt((value) => value + 1)}
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
  active: boolean;
  onReopen: () => void;
}

function ProjectTerminal({ api, recentId, projectName, active, onReopen: reopen }: ProjectTerminalProps) {
  const container = useRef<HTMLDivElement>(null);
  const { status, focus } = useTerminalSession(api, recentId, container);

  useEffect(() => {
    if (active && status.kind === 'running') focus();
  }, [active, status.kind, focus]);

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
