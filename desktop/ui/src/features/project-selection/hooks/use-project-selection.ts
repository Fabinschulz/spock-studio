import { useCallback, useEffect, useState } from 'react';
import { ProjectPickError, type ProjectPickerPort, type RecentProject, type SelectedProject } from '../domain/types';

export function useProjectSelection(port: ProjectPickerPort) {
  const [selected, setSelected] = useState<SelectedProject | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [cancelled, setCancelled] = useState(false);
  const [recents, setRecents] = useState<RecentProject[]>([]);

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
      .then((list) => {
        if (active) setRecents(list);
      })
      .catch(() => undefined);
    return () => {
      active = false;
    };
  }, [port]);

  const pick = useCallback(async (): Promise<SelectedProject | null> => {
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
