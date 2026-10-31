import { useCallback, useEffect, useMemo, useRef, useState } from 'react';

import { scriptDuration } from '@/prototype/domain/script-duration';
import type { ScriptedEvent } from '@/prototype/types/event';
import type { Replay, ReplayOptions } from '@/prototype/types/replay';

export function useReplay(script: ScriptedEvent[], { startAt = 0, autoplay = false }: ReplayOptions = {}): Replay {
  const duration = scriptDuration(script);
  const [clock, setClock] = useState(startAt);
  const [playing, setPlaying] = useState(autoplay);
  const [speed, setSpeed] = useState(1);
  const lastFrame = useRef<number | null>(null);

  useEffect(() => {
    if (!playing) {
      lastFrame.current = null;
      return;
    }

    let frame = 0;
    const tick = (now: number) => {
      const elapsed = lastFrame.current === null ? 0 : now - lastFrame.current;
      lastFrame.current = now;
      let reachedEnd = false;

      setClock((previous) => {
        const next = previous + elapsed * speed;
        reachedEnd = next >= duration;
        return reachedEnd ? duration : next;
      });

      if (reachedEnd) setPlaying(false);
      else frame = requestAnimationFrame(tick);
    };

    frame = requestAnimationFrame(tick);
    return () => cancelAnimationFrame(frame);
  }, [playing, speed, duration]);

  const events = useMemo(() => script.filter((e) => e.t <= clock), [script, clock]);

  const play = useCallback(() => {
    setClock((current) => (current >= duration ? 0 : current));
    setPlaying(true);
  }, [duration]);

  const pause = useCallback(() => setPlaying(false), []);

  const restart = useCallback(() => {
    setClock(0);
    setPlaying(true);
  }, []);

  const seek = useCallback((ms: number) => setClock(Math.max(0, Math.min(duration, ms))), [duration]);

  return { clock, duration, playing, speed, events, finished: clock >= duration, play, pause, restart, seek, setSpeed };
}
