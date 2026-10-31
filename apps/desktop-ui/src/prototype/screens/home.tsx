import { Button } from '@synthra.io/ui-kit';
import { Link, useNavigate } from 'react-router';

import { demoProject, recentProjects } from '../data/fixtures';
import { Stage } from '../shell/stage';
import { Glyph, type GlyphName } from '../ui/glyph';
import { Section, Tag } from '../ui/primitives';

import styles from './screen.module.scss';

const upcoming: { title: string; text: string; glyph: GlyphName }[] = [
  {
    title: 'Importar requisitos',
    text: 'Gera especificações e backlog a partir de documentos existentes.',
    glyph: 'file'
  },
  { title: 'Conectar ferramentas', text: 'Jira, Azure DevOps, GitHub, MCPs e bancos de dados.', glyph: 'plug' },
  { title: 'Modernizar sistema', text: 'Analisa um legado e planeja a modernização.', glyph: 'restart' }
];

export function HomeScreen() {
  const navigate = useNavigate();

  return (
    <Stage bar={<span className={styles.barTitle}>Início</span>}>
      <header className={styles.head}>
        <h1 className={styles.title}>Por onde começamos?</h1>
        <p className={styles.lead}>
          Escolha uma atividade. O Mariliz analisa o projeto, propõe a configuração de engenharia e só altera o que você
          aprovar.
        </p>
      </header>

      <div className={styles.stack}>
        <div className={styles.composer}>
          <Glyph name="folder" size={18} />
          <div className={styles.composerText}>
            <p className={styles.composerName}>{demoProject.name}</p>
            <p className={styles.composerPath}>{demoProject.path}</p>
          </div>
          <Button variant="text" color="inherit" onClick={() => undefined}>
            Trocar
          </Button>
          <Button variant="contained" onClick={() => navigate('/configurar')}>
            Analisar projeto
          </Button>
        </div>
        <p className={styles.note}>A análise inicial é somente de leitura e não altera seus arquivos.</p>
      </div>

      <Section title="Atividades" id="atividades">
        <ul role="list" className={styles.rows}>
          <li>
            <Link to="/configurar" className={styles.row}>
              <span className={`${styles.rowIcon} ${styles.rowAccent}`}>
                <Glyph name="layers" size={18} />
              </span>
              <span>
                <span className={styles.rowTitle}>Configurar projeto</span>
                <span className={styles.rowMeta}> — analisa o repositório e configura o Harness.</span>
              </span>
              <span className={styles.rowEnd}>
                <Glyph name="arrow-right" size={16} />
              </span>
            </Link>
          </li>
          {upcoming.map((item) => (
            <li key={item.title}>
              <div className={`${styles.row} ${styles.rowDisabled}`} aria-disabled="true">
                <span className={styles.rowIcon}>
                  <Glyph name={item.glyph} size={18} />
                </span>
                <span>
                  <span className={styles.rowTitle}>{item.title}</span>
                  <span className={styles.rowMeta}> — {item.text}</span>
                </span>
                <Tag>em breve</Tag>
              </div>
            </li>
          ))}
        </ul>
      </Section>

      <Section title="Projetos recentes" id="recentes">
        <ul role="list" className={styles.rows}>
          {recentProjects.map((project) => (
            <li key={project.id}>
              <Link to="/projeto" className={styles.row}>
                <span className={styles.rowIcon}>
                  <Glyph name="folder" size={18} />
                </span>
                <span>
                  <span className={styles.rowTitle}>{project.name}</span>
                  <span className={`${styles.rowMeta} ${styles.mono}`}> {project.path}</span>
                </span>
                <span className={styles.rowEnd}>{project.when}</span>
              </Link>
            </li>
          ))}
        </ul>
      </Section>
    </Stage>
  );
}
