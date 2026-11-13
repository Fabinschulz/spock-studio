import { useEffect, useState } from 'react';

export function useElapsed(startedAt: number, active: boolean): number {
  const [now, setNow] = useState(startedAt);

  useEffect(() => {
    if (!active) return;
    const id = window.setInterval(() => setNow(Date.now()), 1000);
    return () => window.clearInterval(id);
  }, [active]);

  return Math.max(0, now - startedAt);
}
