import { Alert, ArrowRightIcon, Button } from '@synthra.io/ui-kit';

import { Checklist, PrimaryActionFooter, SectionCard } from '@/shared/components/ui';
import type { Discovery } from '@/shared/schema';

import { SpecificationGlyph } from '../../components/implementation-model-badge';
import styles from '../../components/panels.module.scss';
import type { Loadable } from '../../types/journey';
import { AiEnvironmentSelect } from './ai-environment-select';
import { DiscoveryDetails } from './discovery-details';
import { FolderPickerCard, type PickedProject } from './folder-picker-card';

const ANALYSIS_TOPICS = [
  { text: 'Stack e tecnologias do projeto' },
  { text: 'Ferramentas e dependências' },
  { text: 'Agentes, skills e configurações existentes' },
  { text: 'Alterações locais e conflitos' }
];

interface ProjectStepProps {
  /** `null` enquanto nenhuma pasta foi escolhida. */
  project: PickedProject | null;
  discovery: Loadable<Discovery>;
  aiEnvironment: string | null;
  onEnvironment: (id: string) => void;
  /** Começa a análise: ela nunca roda sozinha. */
  onAnalyze: () => void;
  onContinue: () => void;
}

export function ProjectStep({
  project,
  discovery,
  aiEnvironment,
  onEnvironment,
  onAnalyze,
  onContinue
}: ProjectStepProps) {
  const analyzing = discovery.status === 'loading';
  const ready = project !== null && discovery.status === 'ready';

  return (
    <section className={styles.step}>
      <h2 className="visually-hidden">{project ? `Projeto ${project.name}` : 'Projeto'}</h2>

      <div className={styles.projectWrap}>
        <div className={styles.projectGrid}>
          <FolderPickerCard project={project} analyzing={analyzing} onAnalyze={onAnalyze} />

          <div className={styles.stack}>
            <SectionCard title="O que vamos analisar?" icon={<SpecificationGlyph size={20} />}>
              <Checklist items={ANALYSIS_TOPICS} />
            </SectionCard>
            <AiEnvironmentSelect value={aiEnvironment} disabled={project === null} onChange={onEnvironment} />
          </div>
        </div>
      </div>

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
