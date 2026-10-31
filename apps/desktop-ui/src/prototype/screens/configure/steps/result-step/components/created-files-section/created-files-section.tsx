import { Section } from '@/prototype/components/section';
import screen from '@/prototype/styles/screen.module.scss';

import type { CreatedFilesSectionProps } from './types';

export function CreatedFilesSection({ files }: CreatedFilesSectionProps) {
  return (
    <Section title="Arquivos criados" id="arquivos" aside={`${files.length}`}>
      <details className={screen.details}>
        <summary>Mostrar a lista</summary>
        <ul role="list" className={screen.stack}>
          {files.map((path) => (
            <li key={path} className={screen.mono}>
              {path}
            </li>
          ))}
        </ul>
      </details>
    </Section>
  );
}
