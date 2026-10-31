import { Glyph } from '@/shared/components/ui';

import { ApproveCheckbox } from '@/prototype/components/approve-checkbox';
import { Section } from '@/prototype/components/section';
import { Tag } from '@/prototype/components/tag';
import { CHANGE_ACTION_TEXT, CHANGE_ACTION_TONE } from '@/prototype/constants/review';
import screen from '@/prototype/styles/screen.module.scss';

import { ConflictResolution } from '../conflict-resolution';
import type { FilesSectionProps } from './types';

const isDecidable = (action: string) => action === 'create' || action === 'update' || action === 'remove';

export function FilesSection({ changes, approved, resolutions, onToggle, onResolve }: FilesSectionProps) {
  return (
    <Section title={`Arquivos (${changes.length})`} id="arquivos">
      <ul role="list" className={screen.rows}>
        {changes.map((change) => (
          <li key={change.id}>
            <div className={screen.row}>
              {isDecidable(change.action) ? (
                <ApproveCheckbox
                  checked={approved.includes(change.id)}
                  label={`Aprovar ${CHANGE_ACTION_TEXT[change.action]} ${change.path}`}
                  onChange={() => onToggle(change.id)}
                />
              ) : (
                <span className={screen.rowIcon}>
                  <Glyph name={change.action === 'conflict' ? 'alert' : 'file'} size={16} />
                </span>
              )}
              <span className={screen.minZero}>
                <span className={screen.mono}>{change.path}</span>
                {change.conflictReason ? (
                  <span className={`${screen.rowMeta} ${screen.block}`}>{change.conflictReason}</span>
                ) : null}
              </span>
              <Tag tone={CHANGE_ACTION_TONE[change.action]}>{CHANGE_ACTION_TEXT[change.action]}</Tag>
            </div>
            {change.action === 'conflict' ? (
              <ConflictResolution
                path={change.path}
                value={resolutions[change.id] ?? 'keep'}
                onChange={(value) => onResolve(change.id, value)}
              />
            ) : null}
          </li>
        ))}
      </ul>
    </Section>
  );
}
