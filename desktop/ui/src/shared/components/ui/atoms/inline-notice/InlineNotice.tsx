import { Alert } from '@mariliz/ui-kit';
import type { ReactNode } from 'react';

export type NoticeTone = 'info' | 'warning' | 'danger' | 'success';

const severities = { info: 'info', warning: 'warning', danger: 'error', success: 'success' } as const;

interface InlineNoticeProps {
  tone?: NoticeTone;
  title?: string;
  children: ReactNode;
  className?: string;
}

/** Só o erro interrompe o leitor de tela (`role="alert"`): o `Alert` do MUI anuncia todos por padrão. */
export function InlineNotice({ tone = 'info', title, children, className }: InlineNoticeProps) {
  return (
    <Alert severity={severities[tone]} title={title} className={className} role={tone === 'danger' ? 'alert' : 'note'}>
      {children}
    </Alert>
  );
}
