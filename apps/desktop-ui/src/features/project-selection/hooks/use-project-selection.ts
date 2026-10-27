import { useCallback, useState } from 'react';

import { ProjectPickError, type ProjectPickerPort, type SelectedProject } from '../core/types';

export function useProjectSelection(port: ProjectPickerPort) {
  const [selected, setSelected] = useState<SelectedProject | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const pick = useCallback(async (): Promise<SelectedProject | null> => {
    setBusy(true);
    setError(null);
    try {
      const project = await port.pick();
      if (project) setSelected(project);
      return project;
    } catch (e) {
      setError(e instanceof ProjectPickError ? e.message : 'Não foi possível selecionar a pasta.');
      return null;
    } finally {
      setBusy(false);
    }
  }, [port]);

  return { selected, busy, error, pick };
}
