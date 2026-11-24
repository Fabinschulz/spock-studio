import { formatEngineDate } from '@/shared/utils';
import type { CloudProvider, CloudRead } from '@/shared/schema';

import styles from '../cloud-page.module.scss';
import { capabilityTitle } from '../domain/labels';

export function CloudReadTable({ read, provider }: { read: CloudRead; provider: CloudProvider | undefined }) {
  const title = capabilityTitle(provider, read.capability);
  return (
    <div className={styles.tableWrap}>
      <p className={styles.hint}>
        {title}
        {read.region ? ` em ${read.region}` : ''}, lido {formatEngineDate(read.readAt)}: {read.rows.length}{' '}
        {read.rows.length === 1 ? 'item' : 'itens'}
        {read.truncated ? ' (havia mais)' : ''}.
      </p>
      {read.rows.length > 0 ? (
        <table className={styles.table} aria-label={title}>
          <thead>
            <tr>
              {read.columns.map((column) => (
                <th key={column.id} scope="col">
                  {column.label}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {read.rows.map((row, index) => (
              <tr key={index}>
                {row.map((cell, column) => (
                  <td key={read.columns[column]?.id ?? column}>{cell ?? '-'}</td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      ) : null}
    </div>
  );
}
