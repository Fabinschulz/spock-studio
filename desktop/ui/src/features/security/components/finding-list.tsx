import { StatusChip } from '@/shared/components';
import type { SecurityFinding } from '@/shared/schema';

import styles from '../security-page.module.scss';

const SEVERITY = { high: { tone: 'danger', label: 'Grave' }, medium: { tone: 'warning', label: 'Atenção' } } as const;

export function FindingList({ findings }: { findings: SecurityFinding[] }) {
  return (
    <ul className={styles.findings}>
      {findings.map((finding) => (
        <li key={finding.id} className={styles.finding}>
          <div className={styles.findingHead}>
            <StatusChip tone={SEVERITY[finding.severity].tone}>{SEVERITY[finding.severity].label}</StatusChip>
            <strong>{finding.title}</strong>
          </div>
          <span className={styles.path}>
            {finding.path}
            {finding.line === null ? '' : `, linha ${finding.line}`}
          </span>
          <p className={styles.advice}>{finding.advice}</p>
        </li>
      ))}
    </ul>
  );
}
