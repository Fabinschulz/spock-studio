import { useCallback, useState } from 'react';

export function useCommandPalette() {
  const [open, setOpen] = useState(false);
  const show = useCallback(() => setOpen(true), []);
  const hide = useCallback(() => setOpen(false), []);
  const toggle = useCallback(() => setOpen((value) => !value), []);

  return { open, show, hide, toggle };
}
