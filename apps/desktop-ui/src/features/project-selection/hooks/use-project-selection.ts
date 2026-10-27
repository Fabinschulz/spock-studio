import { useCallback, useState } from 'react';

import { ProjectPickError, type ProjectPickerPort, type SelectedProject } from '../core/types';

export function useProjectSelection(port: ProjectPickerPort) {
  const [selected, setSelected] = useState<SelectedProject | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const pick = useCallback(async () => {
    setBusy(true);
    setError(null);
    try {
      const project = await port.pick();
      if (project) setSelected(project);
    } catch (e) {
      setError(e instanceof ProjectPickError ? e.message : 'Não foi possível selecionar a pasta.');
    } finally {
      setBusy(false);
    }
  }, [port]);

  return { selected, busy, error, pick };
}
