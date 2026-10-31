import styles from './notice.module.scss';
import type { NoticeProps } from './types';

export function Notice({ icon, warn = false, action, children }: NoticeProps) {
  return (
    <div className={`${styles.notice} ${warn ? styles.noticeWarn : ''}`}>
      {icon}
      <div>{children}</div>
      {action}
    </div>
  );
}
