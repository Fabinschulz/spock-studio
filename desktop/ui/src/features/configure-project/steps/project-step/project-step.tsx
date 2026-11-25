import { ArrowRightIcon, Button, LoadingBar } from '@mariliz/ui-kit';

import { InlineNotice, PrimaryActionFooter } from '@/shared/components/ui';
import type { Discovery } from '@/shared/schema';

import styles from '../../components/panels.module.scss';
import { StepHeader } from '../../components/step-header';
import type { Loadable } from '../../types/journey';
import { DiscoveryDetails } from './discovery-details';
import { FolderPickerCard, type PickedProject } from './folder-picker-card';

interface ProjectStepProps {
  project: PickedProject | null;
  discovery: Loadable<Discovery>;
  /** Começa a análise: ela nunca roda sozinha. */
  onAnalyze: () => void;
  onContinue: () => void;
}

function continueHint(project: PickedProject | null, status: Loadable<Discovery>['status']): string | null {
  if (project === null) return 'Escolha a pasta do projeto para continuar.';
  if (status === 'ready' || status === 'loading') return null;
  return 'Analise o projeto para continuar.';
}

export function ProjectStep({ project, discovery, onAnalyze, onContinue }: ProjectStepProps) {
  const analyzing = discovery.status === 'loading';
  const ready = project !== null && discovery.status === 'ready';

  return (
    <section className={styles.step}>
      <StepHeader title={project ? `Analisar ${project.name}` : 'Analisar o projeto'}>
        O Spock Studio lê a estrutura do projeto para entender a stack, os comandos e o que já existe. Nada é alterado.
      </StepHeader>

      <FolderPickerCard project={project} analyzing={analyzing} onAnalyze={onAnalyze} />

      {analyzing ? <LoadingBar aria-label="Analisando o projeto" /> : null}

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
