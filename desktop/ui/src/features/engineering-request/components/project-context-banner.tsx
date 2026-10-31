import type { SelectedProject } from '@/features/project-selection';
import { Typography } from '@synthra.io/ui-kit';

import styles from '../engineering-request-page.module.scss';

export function ProjectContextBanner({ project }: { project: SelectedProject }) {
  return (
    <Typography className={styles.project} variant="body2" component="p">
      Projeto: <strong>{project.displayName}</strong> ({project.displayPath})
    </Typography>
  );
}
