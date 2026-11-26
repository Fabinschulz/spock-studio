import { SelectField } from '@mariliz/ui-kit';

import { PERIOD_WEEKS } from '../hooks/use-period';

const OPTIONS = PERIOD_WEEKS.map((value) => ({ label: `Últimas ${value} semanas`, value }));

export function PeriodField({ weeks, onChange }: { weeks: number; onChange: (weeks: number) => void }) {
  return (
    <SelectField
      label="Período"
      size="small"
      fullWidth={false}
      sx={{ minWidth: '14rem' }}
      value={weeks}
      onChange={(event) => onChange(Number(event.target.value))}
      options={OPTIONS}
    />
  );
}
