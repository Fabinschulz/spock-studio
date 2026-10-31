import { StatusGlyph } from '@/shared/components/ui';

import { Tag } from '@/prototype/components/tag';
import { EVAL_STATUS_GLYPH, EVAL_STATUS_TEXT } from '@/prototype/constants/evals';
import screen from '@/prototype/styles/screen.module.scss';

import type { EvalRowProps } from './types';

export function EvalRow({ result }: EvalRowProps) {
  return (
    <details className={screen.details}>
      <summary className={screen.evalSummary}>
        <StatusGlyph state={EVAL_STATUS_GLYPH[result.status]} label={EVAL_STATUS_TEXT[result.status]} />
        <span className={screen.rowTitle}>{result.name}</span>
        {result.kind === 'model' ? <Tag>juiz por modelo</Tag> : null}
        {result.required ? null : <Tag>opcional</Tag>}
        <span className={`${screen.rowEnd} ${screen.pushRight}`}>{EVAL_STATUS_TEXT[result.status]}</span>
      </summary>
      <div className={screen.evalBody}>
        <p className={screen.rowMeta}>{result.property}</p>
        <p>{result.criteria}</p>
        {result.evidence.length > 0 ? (
          <ul role="list" className={screen.stack}>
            {result.evidence.map((line) => (
              <li key={line} className={screen.mono}>
                {line}
              </li>
            ))}
          </ul>
        ) : null}
        {result.reason ? <p className={screen.rowMeta}>{result.reason}</p> : null}
      </div>
    </details>
  );
}
