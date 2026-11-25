import { ArrowRightIcon, Button, LoadingBar } from '@mariliz/ui-kit';

import { InlineNotice, PrimaryActionFooter } from '@/shared/components/ui';
import type { Discovery } from '@/shared/schema';

import styles from '../../components/panels.module.scss';
import { StepHeader } from '../../components/step-header';
import type { ConfiguredProject } from '../../domain/configured-project';
import type { Loadable } from '../../types/journey';
import type { RunView } from '../../types/run';
import { ConfiguredSummary } from './configured-summary';
import { DiscoveryDetails } from './discovery-details';
import { discoveryHeadline } from './discovery-summary';
import { FolderPickerCard, type PickedProject } from './folder-picker-card';
import { ProjectLine } from './project-line';

interface ProjectStepProps {
  project: PickedProject | null;
  discovery: Loadable<Discovery>;
  /** Começa a análise: ela nunca roda sozinha. */
  onAnalyze: () => void;
  onContinue: () => void;
  /** Só na segunda visita: o que já está aplicado e os atalhos para validar e reverter. */
  configured?: ConfiguredProject | null;
  run?: RunView | null;
  onValidate?: () => void;
  onRevert?: (runId: string) => void;
}

function continueHint(project: PickedProject | null, status: Loadable<Discovery>['status']): string | null {
  if (project === null) return 'Escolha a pasta do projeto para continuar.';
  if (status === 'ready' || status === 'loading') return null;
  return 'Analise o projeto para continuar.';
}

export function ProjectStep({
  project,
  discovery,
  onAnalyze,
  onContinue,
  configured = null,
  run = null,
  onValidate = () => undefined,
  onRevert = () => undefined
}: ProjectStepProps) {
  const analyzing = discovery.status === 'loading';
  const ready = project !== null && discovery.status === 'ready';

  return (
    <section className={styles.step}>
      <StepHeader title={project ? `Analisar ${project.name}` : 'Analisar o projeto'}>
        O Spock Studio lê a estrutura do projeto para entender a stack, os comandos e o que já existe. Nada é alterado.
      </StepHeader>

      {project ? <ProjectLine project={project} /> : <FolderPickerCard />}

      {project && configured ? (
        <ConfiguredSummary configured={configured} run={run} onValidate={onValidate} onRevert={onRevert} />
      ) : null}

      {project ? (
        <div className={styles.analyze}>
          {/* Depois da análise, o primário passa a ser o Próximo: analisar de novo fica como opção. */}
          <Button
            variant={ready ? 'outlined' : 'contained'}
            color={ready ? 'inherit' : 'primary'}
            onClick={onAnalyze}
            disabled={analyzing}
          >
            {analyzing ? 'Analisando…' : ready ? 'Analisar de novo' : 'Analisar projeto'}
          </Button>
        </div>
      ) : null}

      <div role="status" aria-live="polite" className={styles.stack}>
        {analyzing ? (
          <>
            <span className={styles.muted}>Analisando o projeto…</span>
            <LoadingBar aria-label="Analisando o projeto" />
          </>
        ) : null}
        {ready ? <p className={styles.headline}>{discoveryHeadline(discovery.data)}</p> : null}
      </div>

      {discovery.status === 'error' && project ? (
        <InlineNotice tone="danger" title="Não foi possível analisar o projeto">
          {discovery.message}
        </InlineNotice>
      ) : null}

      {ready ? <DiscoveryDetails discovery={discovery.data} /> : null}

      <PrimaryActionFooter hint={continueHint(project, discovery.status)}>
        <Button variant="contained" onClick={onContinue} disabled={!ready} endIcon={<ArrowRightIcon />}>
          Próximo
        </Button>
      </PrimaryActionFooter>
    </section>
  );
}
