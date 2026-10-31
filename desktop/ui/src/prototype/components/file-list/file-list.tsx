import styles from './file-list.module.scss';
import type { FileListProps } from './types';

function Files({ files }: Pick<FileListProps, 'files'>) {
  return (
    <ul role="list" className={styles.files}>
      {files.map((file) => (
        <li key={file} className={styles.file}>
          <span className={styles.fileOp}>criado</span> {file}
        </li>
      ))}
    </ul>
  );
}

export function FileList({ files, collapsible }: FileListProps) {
  if (!collapsible) return <Files files={files} />;

  return (
    <details className={styles.fileDetails}>
      <summary>{files.length} arquivos gravados</summary>
      <Files files={files} />
    </details>
  );
}
