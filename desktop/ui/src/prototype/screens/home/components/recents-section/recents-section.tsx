import { Link } from 'react-router';

import { Glyph } from '@/shared/components/ui';

import { Section } from '@/prototype/components/section';
import { recentProjects } from '@/prototype/data/demo-project';
import screen from '@/prototype/styles/screen.module.scss';

export function RecentsSection() {
  return (
    <Section title="Projetos recentes" id="recentes">
      <ul role="list" className={screen.rows}>
        {recentProjects.map((project) => (
          <li key={project.id}>
            <Link to="/projeto" className={screen.row}>
              <span className={screen.rowIcon}>
                <Glyph name="folder" size={18} />
              </span>
              <span>
                <span className={screen.rowTitle}>{project.name}</span>
                <span className={`${screen.rowMeta} ${screen.mono}`}> {project.path}</span>
              </span>
              <span className={screen.rowEnd}>{project.when}</span>
            </Link>
          </li>
        ))}
      </ul>
    </Section>
  );
}
