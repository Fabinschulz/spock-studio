import { useCallback, useEffect, useRef, useState } from 'react';
import { ProjectPickError, type ProjectPickerPort, type RecentProject, type SelectedProject } from '../domain/types';

export interface ProjectSelectionOptions {
  /**
   * Ao abrir o aplicativo, reabre o projeto mais recente da lista do host, se a pasta ainda estiver acessível. O host
   * ordena os recentes do mais novo ao mais antigo, então o primeiro é o último que a pessoa abriu. Falhar aqui nunca
   * mostra erro: a pessoa só vê o aplicativo sem projeto aberto, como antes.
   */
  restoreLast?: boolean;
}

export function useProjectSelection(port: ProjectPickerPort, { restoreLast = false }: ProjectSelectionOptions = {}) {
  const [selected, setSelected] = useState<SelectedProject | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [cancelled, setCancelled] = useState(false);
  const [recents, setRecents] = useState<RecentProject[]>([]);
  // Uma escolha da pessoa sempre vence a restauração, mesmo que esta termine depois.
  const userActed = useRef(false);
  const restored = useRef(false);

  const refreshRecents = useCallback(async () => {
    if (!port.recents) return;
    try {
      setRecents(await port.recents());
    } catch {
      setRecents([]);
    }
  }, [port]);

  // A lista vem do host; fora do aplicativo desktop a porta não a oferece e ela fica vazia.
  useEffect(() => {
    if (!port.recents) return;
    let active = true;
    port
      .recents()
      .then(async (list) => {
        if (!active) return;
        setRecents(list);

        const last = list[0];
        if (!restoreLast || restored.current || userActed.current) return;
        if (!last?.available || !port.openRecent) return;
        restored.current = true;
        try {
          const project = await port.openRecent(last.id);
          if (active && !userActed.current) setSelected(project);
        } catch {
          // Sem projeto aberto é o estado normal de quem acaba de abrir o aplicativo.
        }
      })
      .catch(() => undefined);
    return () => {
      active = false;
    };
  }, [port, restoreLast]);

  const pick = useCallback(async (): Promise<SelectedProject | null> => {
    userActed.current = true;
    setBusy(true);
    setError(null);
    setCancelled(false);
    try {
      const project = await port.pick();
      if (project) {
        setSelected(project);
        void refreshRecents();
      } else setCancelled(true);
      return project;
    } catch (e) {
      setError(e instanceof ProjectPickError ? e.message : 'Não foi possível selecionar a pasta.');
      return null;
    } finally {
      setBusy(false);
    }
  }, [port, refreshRecents]);

  const openRecent = useCallback(
    async (id: string): Promise<SelectedProject | null> => {
      if (!port.openRecent) return null;
      userActed.current = true;
      setBusy(true);
      setError(null);
      setCancelled(false);
      try {
        const project = await port.openRecent(id);
        setSelected(project);
        void refreshRecents();
        return project;
      } catch (e) {
        setError(e instanceof ProjectPickError ? e.message : 'Não foi possível abrir o projeto.');
        void refreshRecents();
        return null;
      } finally {
        setBusy(false);
      }
    },
    [port, refreshRecents]
  );

  return { selected, busy, error, cancelled, recents, pick, openRecent };
}
