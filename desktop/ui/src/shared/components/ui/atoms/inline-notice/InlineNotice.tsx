import { cn } from '@/shared/utils';
import type { ReactNode } from 'react';
import styles from './InlineNotice.module.scss';

export type NoticeTone = 'info' | 'warning' | 'danger' | 'success';

const glyphs: Record<NoticeTone, ReactNode> = {
  info: (
    <>
      <circle cx="10" cy="10" r="8" />
      <path d="M10 9v5M10 6.2v.1" />
    </>
  ),
  warning: (
    <>
      <path d="M10 2.8 18 16.5H2L10 2.8Z" />
      <path d="M10 8v4M10 14.2v.1" />
    </>
  ),
  danger: (
    <>
      <circle cx="10" cy="10" r="8" />
      <path d="M10 6v5M10 13.8v.1" />
    </>
  ),
  success: (
    <>
      <circle cx="10" cy="10" r="8" />
      <path d="m6.5 10.2 2.4 2.4 4.6-5" />
    </>
  )
};

interface InlineNoticeProps {
  tone?: NoticeTone;
  title?: string;
  children: ReactNode;
  className?: string;
}

export function InlineNotice({ tone = 'info', title, children, className }: InlineNoticeProps) {
  return (
    <div className={cn(styles.notice, styles[tone], className)} role={tone === 'danger' ? 'alert' : undefined}>
      <svg
        className={styles.icon}
        width="20"
        height="20"
        viewBox="0 0 20 20"
        fill="none"
        stroke="currentColor"
        strokeWidth="1.6"
        strokeLinecap="round"
        strokeLinejoin="round"
        aria-hidden="true"
        focusable="false"
      >
        {glyphs[tone]}
      </svg>
      <div className={styles.text}>
        {title ? <p className={styles.title}>{title}</p> : null}
        <div className={styles.body}>{children}</div>
      </div>
    </div>
  );
}
