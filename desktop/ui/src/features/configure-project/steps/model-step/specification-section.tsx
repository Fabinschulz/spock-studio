import type { Discovery } from '@/shared/schema';

import { specificationStateLabel } from '../../domain/labels';
import styles from './model.module.scss';
import { SpecificationDependencies } from './specification-dependencies';
import { SpecificationFooter } from './specification-footer';

type Specification = Discovery['specification'];

function installationText({ version, versionTested }: Specification): string {
  if (!version) return 'Será instalada pelo instalador oficial, só se você aprovar o comando na revisão.';
  return versionTested
    ? `Versão ${version}, validada para esta fase.`
    : `Versão ${version}, ainda não validada por esta fase.`;
}

interface SpecificationSectionProps {
  specification: Specification;
  tools: Discovery['tools'];
  updateSpecification: boolean;
  allowReinstall: boolean;
  onUpdateSpecification: (value: boolean) => void;
  onReinstall: (value: boolean) => void;
}

export function SpecificationSection({
  specification,
  tools,
  updateSpecification,
  allowReinstall,
  onUpdateSpecification,
  onReinstall
}: SpecificationSectionProps) {
  return (
    <section aria-labelledby="specification-title" className={styles.section}>
      <h3 id="specification-title" className={styles.title}>
        Estrutura de especificação
      </h3>
      <p className={styles.note}>
        Organiza as especificações do projeto em um ciclo guiado (especificar, desenhar, planejar, executar e validar),
        com foco em qualidade, rastreabilidade e entrega contínua.
      </p>
      <div className={styles.line}>
        <span className={styles.name}>{specificationStateLabel[specification.state]}</span>
        <span className={styles.end}>{installationText(specification)}</span>
      </div>
      <SpecificationDependencies tools={tools} />
      <SpecificationFooter
        state={specification.state}
        updateSpecification={updateSpecification}
        allowReinstall={allowReinstall}
        onUpdateSpecification={onUpdateSpecification}
        onReinstall={onReinstall}
      />
    </section>
  );
}
