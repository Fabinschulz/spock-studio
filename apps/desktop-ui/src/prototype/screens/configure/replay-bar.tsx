import { formatClock } from '../../domain/describe-event';
import type { Replay } from '../../hooks/use-replay';
import shell from '../../shell/shell.module.scss';
import { Glyph } from '../../ui/glyph';
import { Tag } from '../../ui/primitives';
import screen from '../screen.module.scss';

import styles from './operation.module.scss';

export type Contract = 'current' | 'proposed';

interface Props {
  replay: Replay;
  contract: Contract;
  onContract: (contract: Contract) => void;
}

/**
 * Controle da reprodução de demonstração. Existe só no protótipo: na aplicação real não há relógio nem script,
 * os eventos chegam do motor quando acontecem.
 */
export function ReplayBar({ replay, contract, onContract }: Props) {
  return (
    <div className={styles.replay} role="group" aria-label="Controle da reprodução de demonstração">
      <span className={styles.replayLabel}>
        <Tag tone="warning">demonstração</Tag> Reprodução gravada
      </span>

      <span className={styles.replayControls}>
        <button
          type="button"
          className={shell.iconButton}
          aria-label={replay.playing ? 'Pausar reprodução' : 'Reproduzir'}
          onClick={replay.playing ? replay.pause : replay.play}
        >
          <Glyph name={replay.playing ? 'pause' : 'play'} size={15} />
        </button>
        <button type="button" className={shell.iconButton} aria-label="Reiniciar reprodução" onClick={replay.restart}>
          <Glyph name="restart" size={15} />
        </button>
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
          <option value={1}>1×</option>
          <option value={2}>2×</option>
          <option value={4}>4×</option>
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
