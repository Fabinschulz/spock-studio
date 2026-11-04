import { Alert, ArrowRightIcon, Button } from '@synthra.io/ui-kit';

import { PrimaryActionFooter } from '@/shared/components/ui';
import type { Discovery } from '@/shared/schema';

import styles from '../../components/panels.module.scss';
import { StepHeader } from '../../components/step-header';
import type { Loadable } from '../../types/journey';
import { DiscoveryDetails } from './discovery-details';
import { FolderPickerCard, type PickedProject } from './folder-picker-card';

interface ProjectStepProps {
  /** `null` enquanto nenhuma pasta foi escolhida. */
  project: PickedProject | null;
  discovery: Loadable<Discovery>;
  /** Começa a análise: ela nunca roda sozinha. */
  onAnalyze: () => void;
  onContinue: () => void;
}

export function ProjectStep({ project, discovery, onAnalyze, onContinue }: ProjectStepProps) {
  const analyzing = discovery.status === 'loading';
  const ready = project !== null && discovery.status === 'ready';

  return (
    <section className={styles.step}>
      <StepHeader title={project ? `Analisar ${project.name}` : 'Analisar o projeto'}>
        O Mariliz lê a estrutura do projeto para entender a stack, os comandos e o que já existe. Nada é alterado.
      </StepHeader>

      <FolderPickerCard project={project} analyzing={analyzing} onAnalyze={onAnalyze} />

      {analyzing ? <div aria-hidden="true" className={styles.progress} /> : null}

      {discovery.status === 'error' && project ? (
        <Alert severity="error" title="Não foi possível analisar o projeto" description={discovery.message} />
      ) : null}

      {ready ? <DiscoveryDetails discovery={discovery.data} /> : null}

      <PrimaryActionFooter>
        <Button variant="contained" onClick={onContinue} disabled={!ready} endIcon={<ArrowRightIcon />}>
          Próximo
        </Button>
      </PrimaryActionFooter>
    </section>
  );
}
