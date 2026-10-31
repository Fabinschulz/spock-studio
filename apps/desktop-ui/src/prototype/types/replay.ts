import type { ScriptedEvent } from './event';

export interface ReplayOptions {
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
