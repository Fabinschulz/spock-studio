import { formatEngineDate } from '@/shared/utils';
import { useAction } from '@/shared/hooks';
import { Radio, StatusChip } from '@/shared/components';
import type { IacDriftScheduleView } from '@/shared/schema';
import { Button, Checkbox } from '@mariliz/ui-kit';
import { useState } from 'react';

import styles from '../infrastructure-page.module.scss';
import type { IacApi } from '../service/iac-api';

const INTERVAL_LABEL: Record<number, string> = {
  6: 'A cada 6 h',
  12: 'A cada 12 h',
  24: 'Uma vez por dia',
  168: 'Uma vez por semana'
};

interface DriftScheduleProps {
  api: IacApi;
  projectId: string;
  view: IacDriftScheduleView;
  /** Pasta, ambiente e conexão escolhidos acima: a verificação agendada usa os mesmos. */
  target: { folder: string; workspace?: string; connectionId?: string };
  disabled: boolean;
  onSaved: () => void;
}

/** Verificação automática de drift do projeto. Roda enquanto o Spock está aberto, só lê a nuvem e entra no histórico. */
export function DriftSchedule({ api, projectId, view, target, disabled, onSaved }: DriftScheduleProps) {
  const action = useAction(onSaved);
  const current = view.schedule;
  const [enabled, setEnabled] = useState(current?.enabled ?? false);
  const [selected, setSelected] = useState(current?.intervalHours ?? 24);

  return (
    <fieldset className={styles.choices} aria-label="Verificação automática de drift">
      <legend>Verificação automática de drift</legend>
      <p className={styles.hint}>
        {current?.enabled ? (
          <>
            <StatusChip tone="info">Ligada</StatusChip>{' '}
            {INTERVAL_LABEL[current.intervalHours] ?? `${current.intervalHours} h`}, pasta {current.folder ?? 'padrão'},
            ambiente {current.workspace ?? 'default'}
            {view.nextRunAt ? `; próxima em ${formatEngineDate(view.nextRunAt)}` : ''}
            {current.lastRunAt ? `; última em ${formatEngineDate(current.lastRunAt)}` : ''}.
          </>
        ) : (
          'Desligada.'
        )}{' '}
        Roda enquanto o Spock está aberto, só lê a nuvem e usa a pasta, o ambiente e as credenciais escolhidos acima.
      </p>
      {current?.lastError ? <p className={styles.error}>Última tentativa: {current.lastError}</p> : null}
      <Checkbox
        label="Verificar drift automaticamente"
        checked={enabled}
        disabled={disabled || action.busy}
        onChange={(_, checked) => setEnabled(checked)}
      />
      {enabled
        ? view.intervals.map((hours) => (
            <Radio
              key={hours}
              name="iac-drift-interval"
              value={String(hours)}
              checked={selected === hours}
              disabled={disabled || action.busy}
              onChange={() => setSelected(hours)}
              label={INTERVAL_LABEL[hours] ?? `${hours} h`}
            />
          ))
        : null}
      <div className={styles.actions}>
        <Button
          type="button"
          variant="outlined"
          disabled={disabled || action.busy}
          onClick={() =>
            void action.run(
              () => api.setDriftSchedule({ projectId, enabled, intervalHours: selected, ...target }),
              'O agendamento não foi salvo.',
              {
                success: (saved) =>
                  saved.schedule?.enabled ? 'Verificação automática ligada.' : 'Verificação automática desligada.'
              }
            )
          }
        >
          Salvar agendamento
        </Button>
      </div>
      {action.error ? (
        <p className={styles.error} role="alert">
          {action.error}
        </p>
      ) : null}
      {action.notice ? <p className={styles.hint}>{action.notice.text}</p> : null}
    </fieldset>
  );
}
