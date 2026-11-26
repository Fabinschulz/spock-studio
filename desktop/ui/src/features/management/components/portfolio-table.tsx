import { StatusChip } from '@/shared/components';
import { pathTo } from '@/shared/routing';
import type { PortfolioProject } from '@/shared/schema';
import { formatEngineDate, plural } from '@/shared/utils';
import { Button } from '@mariliz/ui-kit';
import { Link } from 'react-router';

import type { OpenAction } from '../domain/open-project';
import { attentionSignals } from '../domain/portfolio-signals';
import styles from './portfolio-table.module.scss';

const KIND_LABEL: Record<string, string> = { issues: 'Issues', pullRequests: 'Pull requests' };
const COLUMNS = 5;

interface PortfolioTableProps {
  projects: PortfolioProject[];
  weeks: number;
  actionOf: (project: PortfolioProject) => OpenAction;
  busy: boolean;
  onOpen: (recentId: string) => void;
}

/**
 * Um projeto por grupo, com os mesmos cabeçalhos de coluna para todos. Tipos diferentes (issues, pull requests) ficam em
 * linhas separadas: não se somam. O que o motor não sabe aparece como "sem dado", nunca como zero.
 */
export function PortfolioTable({ projects, weeks, actionOf, busy, onOpen }: PortfolioTableProps) {
  return (
    <table className={styles.table}>
      <caption className="visually-hidden">Projetos, de quem mais pede atenção a quem menos pede</caption>
      <thead>
        <tr>
          <th scope="col">Tipo</th>
          <th scope="col" className={styles.number}>
            Abertos
          </th>
          <th scope="col" className={styles.number}>
            Em andamento
          </th>
          <th scope="col" className={styles.number}>
            Entregues em {weeks} semanas
          </th>
          <th scope="col" className={styles.number}>
            Prazo vencido
          </th>
        </tr>
      </thead>
      {projects.map((project) => (
        <ProjectGroup
          key={project.projectId}
          project={project}
          action={actionOf(project)}
          busy={busy}
          onOpen={onOpen}
        />
      ))}
    </table>
  );
}

interface ProjectGroupProps {
  project: PortfolioProject;
  action: OpenAction;
  busy: boolean;
  onOpen: (recentId: string) => void;
}

function ProjectGroup({ project, action, busy, onOpen }: ProjectGroupProps) {
  const signals = attentionSignals(project.signals);
  const withoutTool = project.sources === 0;

  return (
    <tbody className={styles.group}>
      <tr>
        <th scope="rowgroup" colSpan={COLUMNS} className={styles.name}>
          {project.displayName}
          <span className={styles.path}>{project.displayPath}</span>
        </th>
      </tr>
      <tr>
        <td colSpan={COLUMNS} className={styles.state}>
          <div className={styles.line}>
            <span className={styles.status}>
              {withoutTool ? (
                <>
                  <StatusChip tone="neutral">Sem ferramenta</StatusChip>
                  <Link to={pathTo('integrations')}>Vincular uma ferramenta</Link>
                </>
              ) : signals.length === 0 ? (
                <StatusChip tone="success">Sem pontos de atenção</StatusChip>
              ) : null}
            </span>
            <OpenControl project={project} action={action} busy={busy} onOpen={onOpen} />
          </div>
          {signals.length > 0 ? (
            <ul className={styles.signals} aria-label={`Pontos de atenção de ${project.displayName}`}>
              {signals.map((signal) => (
                <li key={signal.id}>{signal.message}</li>
              ))}
            </ul>
          ) : null}
          {project.sources > 0 ? (
            <p className={styles.source}>
              {plural(project.sources, 'fonte', 'fontes')}
              {project.oldestReadAt ? ` · leitura mais antiga ${formatEngineDate(project.oldestReadAt)}` : ''}
            </p>
          ) : null}
        </td>
      </tr>
      {project.kinds.map((kind) => (
        <tr key={kind.kind}>
          <th scope="row">{KIND_LABEL[kind.kind] ?? kind.kind}</th>
          <td className={styles.number}>{kind.open}</td>
          <td className={styles.number}>{kind.inProgress}</td>
          <td className={styles.number}>{kind.deliveredInPeriod}</td>
          {kind.overdue === null ? (
            <td className={styles.absent}>sem dado</td>
          ) : (
            <td className={styles.number}>{kind.overdue}</td>
          )}
        </tr>
      ))}
    </tbody>
  );
}

function OpenControl({ project, action, busy, onOpen }: ProjectGroupProps) {
  if (action?.kind === 'current')
    return (
      <span className={styles.open}>
        <StatusChip tone="info">Aberto agora</StatusChip>
        <Link to={pathTo('management')} aria-label={`Ver acompanhamento de ${project.displayName}`}>
          Ver acompanhamento
        </Link>
      </span>
    );
  if (action?.kind === 'recent')
    return (
      <Button
        type="button"
        variant="outlined"
        size="small"
        aria-label={`Abrir projeto ${project.displayName}`}
        aria-disabled={busy || undefined}
        onClick={busy ? undefined : () => onOpen(action.recentId)}
      >
        Abrir projeto
      </Button>
    );
  return <span className={styles.source}>Fora dos recentes: abra a pasta dele pelo seletor do topo.</span>;
}
