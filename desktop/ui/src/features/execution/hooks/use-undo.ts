import { errorMessage, type EngineClient } from '@/shared/api';
import { isTerminalState } from '@/shared/schema';
import { useCallback, useEffect, useState } from 'react';

import type { RunView } from '../domain/run-timeline';
import { readUndoState, revertRun } from '../service/execution-api';

export type Undo =
  | { status: 'none' }
  | { status: 'available' }
  | { status: 'reverting' }
  | { status: 'reverted' }
  | { status: 'partial' }
  | { status: 'failed'; message: string };

/**
 * Só uma execução terminada que gravou algo pela ação aprovada tem o que desfazer. Quem diz se dá é o diário do motor,
 * lido de novo depois de desfazer: um arquivo mudado por alguém depois da ação fica, e a tela conta isso.
 */
export function useUndo(client: EngineClient, runId: string, view: RunView | null) {
  const [undo, setUndo] = useState<Undo>({ status: 'none' });
  const projectId = view?.snapshot.projectId ?? null;
  const finished = view !== null && isTerminalState(view.snapshot.state);
  const wrote = view?.snapshot.result?.actions?.some((action) => action.status === 'executed') === true;

  useEffect(() => {
    if (!finished || !wrote || projectId === null) return;
    let active = true;
    readUndoState(client, projectId, runId)
      .then((state) => {
        if (!active) return;
        setUndo(
          state.reverted ? { status: 'reverted' } : state.canRevert ? { status: 'available' } : { status: 'none' }
        );
      })
      .catch(() => {
        // Sem o diário, a tela não oferece desfazer: melhor faltar o botão que oferecer o que o motor vai recusar.
        if (active) setUndo({ status: 'none' });
      });
    return () => {
      active = false;
    };
  }, [client, finished, wrote, projectId, runId]);

  const revert = useCallback(async () => {
    if (projectId === null) return;
    setUndo({ status: 'reverting' });
    try {
      await revertRun(client, projectId, runId);
      const after = await readUndoState(client, projectId, runId);
      setUndo(after.reverted ? { status: 'reverted' } : { status: 'partial' });
    } catch (cause) {
      setUndo({ status: 'failed', message: errorMessage(cause, 'Não foi possível desfazer.') });
    }
  }, [client, projectId, runId]);

  return { undo, revert };
}
