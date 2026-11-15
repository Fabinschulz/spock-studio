import type { SelectedProject } from '@/features/project-selection';
import { StatusChip } from '@/shared/components';
import type { IacEngine, IacRuntimeSource, IacRuntimeStatus } from '@/shared/schema';
import { Button } from '@mariliz/ui-kit';

import styles from '../infrastructure-page.module.scss';
import { installationName } from '../domain/labels';

interface ProjectTerraformProps {
  project: SelectedProject;
  status: IacRuntimeStatus;
  busy: boolean;
  onPin: (choice: { engine: IacEngine; version: string; source: IacRuntimeSource } | null) => void;
}

/**
 * A ferramenta e a versão de cada projeto: automáticas (OpenTofu antes de Terraform) até o primeiro uso ou até a pessoa
 * fixar; uma instalação nova nunca troca sozinha.
 */
export function ProjectRuntime({ project, status, busy, onPin }: ProjectTerraformProps) {
  const details = status.project;
  if (!details) return null;
  const usable = status.installations.filter(
    (installation) =>
      installation.verified && installation.compatible && (installation.source === 'managed' || installation.adopted)
  );

  return (
    <div className={styles.panel}>
      <div className={styles.rowHead}>
        <span className={styles.rowTitle}>{project.displayName}</span>
        {details.selected ? (
          <StatusChip tone="success">
            {installationName(details.selected)} · {details.automatic ? 'automático' : 'fixado'}
          </StatusChip>
        ) : (
          <StatusChip tone="warning">Sem ferramenta disponível</StatusChip>
        )}
      </div>
      {details.requirements.length === 0 ? (
        <p className={styles.meta}>Nenhum required_version nos arquivos .tf deste projeto.</p>
      ) : (
        <ul className={styles.facts}>
          {details.requirements.map((requirement) => (
            <li key={`${requirement.file}-${requirement.constraint}`}>
              <code>{requirement.file}</code> exige <code>{requirement.constraint}</code>
              {requirement.valid ? '' : ' (não reconhecida)'}
            </li>
          ))}
        </ul>
      )}
      {details.problem ? <p className={styles.error}>{details.problem}</p> : null}
      {details.automatic ? (
        <p className={styles.hint}>
          A versão automática vira fixa no primeiro uso, para que uma instalação nova não mude este projeto sem você.
        </p>
      ) : null}
      {usable.length > 0 ? (
        <div
          className={styles.actions}
          role="group"
          aria-label={`Ferramenta de infraestrutura de ${project.displayName}`}
        >
          {usable.map((installation) => {
            const current =
              details.pin?.engine === installation.engine &&
              details.pin.version === installation.version &&
              details.pin.source === installation.source;
            return (
              <Button
                key={`${installation.engine}-${installation.source}-${installation.version}`}
                type="button"
                variant={current ? 'contained' : 'outlined'}
                aria-pressed={current}
                disabled={busy || current}
                onClick={() =>
                  onPin({ engine: installation.engine, version: installation.version, source: installation.source })
                }
              >
                Fixar {installationName(installation)}
                {installation.source === 'external' ? ' (externo)' : ''}
              </Button>
            );
          })}
          {details.pin ? (
            <Button type="button" variant="text" disabled={busy} onClick={() => onPin(null)}>
              Voltar para automático
            </Button>
          ) : null}
        </div>
      ) : null}
    </div>
  );
}
