import { StatusGlyph } from '@/shared/components/ui';

import { Notice } from '@/prototype/components/notice';
import { summarizeResult } from '@/prototype/domain/summarize-result';

import type { OutcomeNoticeProps } from './types';

export function OutcomeNotice({ result, evals }: OutcomeNoticeProps) {
  const passed = evals.filter((e) => e.status === 'passed').length;
  const notRun = evals.filter((e) => e.status === 'not_run').length;
  const notRunText = notRun > 0 ? `; ${notRun} não executada${notRun > 1 ? 's' : ''} nesta validação.` : '.';

  return (
    <Notice icon={<StatusGlyph state="done" label="concluído" />}>
      <p>
        <strong>Concluído</strong> · {summarizeResult(result)}
      </p>
      <p>
        {passed} de {evals.length} verificações aprovadas
        {notRunText}
      </p>
    </Notice>
  );
}
