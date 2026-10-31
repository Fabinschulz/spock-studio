import { useCallback, useState } from 'react';

const WIDE_WINDOW = '(min-width: 1180px)';

const fitsBesideContent = () => typeof window.matchMedia === 'function' && window.matchMedia(WIDE_WINDOW).matches;

export function usePanelOpen() {
  const [open, setOpen] = useState(fitsBesideContent);
  const toggle = useCallback(() => setOpen((value) => !value), []);
  return { open, toggle };
}
