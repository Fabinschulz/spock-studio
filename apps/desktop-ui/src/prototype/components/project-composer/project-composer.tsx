import { Glyph } from '@/shared/components/ui';

import { demoProject } from '@/prototype/data/demo-project';
import screen from '@/prototype/styles/screen.module.scss';

import type { ProjectComposerProps } from './types';

export function ProjectComposer({ children }: ProjectComposerProps) {
  return (
    <div className={screen.composer}>
      <Glyph name="folder" size={18} />
      <div className={screen.composerText}>
        <p className={screen.composerName}>{demoProject.name}</p>
        <p className={screen.composerPath}>{demoProject.path}</p>
      </div>
      {children}
    </div>
  );
}
