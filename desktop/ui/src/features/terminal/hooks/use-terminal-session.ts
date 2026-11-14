import { errorMessage } from '@/shared/api';
import { FitAddon } from '@xterm/addon-fit';
import { Terminal } from '@xterm/xterm';
import { useCallback, useEffect, useRef, useState, type RefObject } from 'react';

import { terminalTheme } from '../domain/terminal-theme';
import type { TerminalApi } from '../service/terminal-api';

export type SessionStatus =
  | { kind: 'starting' }
  | { kind: 'running'; shell: string }
  | { kind: 'exited'; code: number | null }
  | { kind: 'failed'; message: string };

const ENDED_NOTE = '\r\n\x1b[90m[terminal encerrado]\x1b[0m\r\n';

/**
 * Uma sessão enquanto o componente existe: quem usa remonta para trocar de projeto ou abrir de novo, e a anterior é
 * encerrada no host. A tela fica escondida quando o painel fecha, mas a sessão continua; só um tamanho visível é
 * repassado ao shell.
 */
export function useTerminalSession(api: TerminalApi, recentId: string, container: RefObject<HTMLDivElement | null>) {
  const [status, setStatus] = useState<SessionStatus>({ kind: 'starting' });
  const terminal = useRef<Terminal | null>(null);
  const fit = useRef<FitAddon | null>(null);

  useEffect(() => {
    const element = container.current;
    if (!element) return;

    const view = new Terminal({
      fontFamily: getComputedStyle(element).getPropertyValue('--font-mono').trim() || 'monospace',
      fontSize: 13,
      cursorBlink: true,
      scrollback: 5000,
      minimumContrastRatio: 4.5,
      theme: terminalTheme()
    });
    const fitter = new FitAddon();
    view.loadAddon(fitter);
    view.open(element);
    const fitVisible = () => {
      if (element.clientWidth > 0 && element.clientHeight > 0) fitter.fit();
    };
    fitVisible();
    terminal.current = view;
    fit.current = fitter;

    let sessionId: string | null = null;
    let disposed = false;

    api
      .open(recentId, { cols: view.cols, rows: view.rows }, (event) => {
        if (event.kind === 'output') {
          view.write(event.data);
          return;
        }
        sessionId = null;
        view.write(ENDED_NOTE);
        setStatus({ kind: 'exited', code: event.code });
      })
      .then(
        (opened) => {
          if (disposed) {
            void api.close(opened.sessionId).catch(() => undefined);
            return;
          }
          sessionId = opened.sessionId;
          setStatus({ kind: 'running', shell: opened.shell });
        },
        (error: unknown) => {
          if (!disposed)
            setStatus({ kind: 'failed', message: errorMessage(error, 'Não foi possível abrir o terminal.') });
        }
      );

    const typed = view.onData((data) => {
      if (sessionId) void api.write(sessionId, data).catch(() => undefined);
    });
    const resized = view.onResize((size) => {
      if (sessionId) void api.resize(sessionId, size).catch(() => undefined);
    });
    const observer = new ResizeObserver(fitVisible);
    observer.observe(element);

    return () => {
      disposed = true;
      observer.disconnect();
      typed.dispose();
      resized.dispose();
      if (sessionId) void api.close(sessionId).catch(() => undefined);
      view.dispose();
      terminal.current = null;
      fit.current = null;
    };
  }, [api, recentId, container]);

  /** Ao reaparecer, o terminal recalcula o tamanho e recebe o foco para a pessoa já digitar. */
  const focus = useCallback(() => {
    fit.current?.fit();
    terminal.current?.focus();
  }, []);

  return { status, focus };
}
