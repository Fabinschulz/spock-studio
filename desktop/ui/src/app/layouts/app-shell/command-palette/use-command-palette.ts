import { useCallback, useState } from 'react';

import { usePaletteShortcut } from './use-palette-shortcut';

export function useCommandPalette() {
  const [open, setOpen] = useState(false);
  const show = useCallback(() => setOpen(true), []);
  const hide = useCallback(() => setOpen(false), []);
  usePaletteShortcut(useCallback(() => setOpen((value) => !value), []));

  return { open, show, hide };
}
