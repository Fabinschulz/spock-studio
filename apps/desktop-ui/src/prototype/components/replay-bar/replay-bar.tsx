import { Glyph } from '@/shared/components/ui';

import { IconButton } from '@/prototype/components/icon-button';
import { Tag } from '@/prototype/components/tag';
import { REPLAY_SPEEDS } from '@/prototype/constants/replay';
import { formatClock } from '@/prototype/domain/format-clock';
import screen from '@/prototype/styles/screen.module.scss';

import styles from './replay-bar.module.scss';
import type { ReplayBarProps } from './types';

// Só existe no protótipo: na aplicação real não há relógio nem script.
export function ReplayBar({ replay, contract, onContract }: ReplayBarProps) {
  return (
    <div className={styles.replay} role="group" aria-label="Controle da reprodução de demonstração">
      <span className={styles.replayLabel}>
        <Tag tone="warning">demonstração</Tag> Reprodução gravada
      </span>

      <span className={styles.replayControls}>
        <IconButton
          aria-label={replay.playing ? 'Pausar reprodução' : 'Reproduzir'}
          onClick={replay.playing ? replay.pause : replay.play}
        >
          <Glyph name={replay.playing ? 'pause' : 'play'} size={15} />
        </IconButton>
        <IconButton aria-label="Reiniciar reprodução" onClick={replay.restart}>
          <Glyph name="restart" size={15} />
        </IconButton>
      </span>

      <input
        className={styles.scrub}
        type="range"
        min={0}
        max={replay.duration}
        step={100}
        value={Math.round(replay.clock)}
        aria-label="Posição da reprodução"
        aria-valuetext={`${formatClock(replay.clock)} de ${formatClock(replay.duration)}`}
        onChange={(e) => replay.seek(Number(e.target.value))}
      />

      <label>
        <span className="mz-vh">Velocidade</span>
        <select
          className={styles.select}
          value={replay.speed}
          onChange={(e) => replay.setSpeed(Number(e.target.value))}
        >
          {REPLAY_SPEEDS.map((speed) => (
            <option key={speed} value={speed}>
              {speed}×
            </option>
          ))}
        </select>
      </label>

      <div className={screen.segmented} role="group" aria-label="Contrato de eventos">
        <button type="button" aria-pressed={contract === 'current'} onClick={() => onContract('current')}>
          Contrato atual
        </button>
        <button type="button" aria-pressed={contract === 'proposed'} onClick={() => onContract('proposed')}>
          Contrato proposto
        </button>
      </div>
    </div>
  );
}
