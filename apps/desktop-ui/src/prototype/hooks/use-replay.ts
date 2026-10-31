import { useCallback, useEffect, useMemo, useRef, useState } from 'react';

import { scriptDuration, type ScriptedEvent } from '../data/demo-scripts';

interface ReplayOptions {
  /** Posição inicial (ms). Usado para abrir a demonstração num instante conhecido. */
  startAt?: number;
  autoplay?: boolean;
}

export interface Replay {
  clock: number;
  duration: number;
  playing: boolean;
  speed: number;
  events: ScriptedEvent[];
  finished: boolean;
  play: () => void;
  pause: () => void;
  restart: () => void;
  seek: (ms: number) => void;
  setSpeed: (value: number) => void;
}

/** Reprodutor da demonstração: avança um relógio e libera os eventos cujo `t` já passou. Não existe na aplicação real. */
export function useReplay(script: ScriptedEvent[], { startAt = 0, autoplay = false }: ReplayOptions = {}): Replay {
  const duration = scriptDuration(script);
  const [clock, setClock] = useState(startAt);
  const [playing, setPlaying] = useState(autoplay);
  const [speed, setSpeed] = useState(1);
  const last = useRef<number | null>(null);

  useEffect(() => {
    if (!playing) {
      last.current = null;
      return;
    }
    let frame = 0;
    const tick = (now: number) => {
      const dt = last.current === null ? 0 : now - last.current;
      last.current = now;
      let reachedEnd = false;
      setClock((prev) => {
        const next = prev + dt * speed;
        if (next >= duration) {
          reachedEnd = true;
          return duration;
        }
        return next;
      });
      if (reachedEnd) setPlaying(false);
      else frame = requestAnimationFrame(tick);
    };
    frame = requestAnimationFrame(tick);
    return () => cancelAnimationFrame(frame);
  }, [playing, speed, duration]);

  const events = useMemo(() => script.filter((e) => e.t <= clock), [script, clock]);

  const play = useCallback(() => {
    setClock((c) => (c >= duration ? 0 : c));
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
