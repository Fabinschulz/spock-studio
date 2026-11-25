import { useId } from 'react';

import { Disclosure, Radio } from '@/shared/components/ui';
import type { Discovery } from '@/shared/schema';

import panels from '../../components/panels.module.scss';
import rows from '../../components/rows.module.scss';
import { specificationStateLabel } from '../../domain/labels';
import { missingDependencies, SpecificationDependencies } from './specification-dependencies';
import { SpecificationFooter } from './specification-footer';

type Specification = Discovery['specification'];

function versionText({ version, versionTested }: Specification): string | null {
  if (!version) return null;
  return versionTested
    ? `Versão ${version}, validada para esta fase.`
    : `Versão ${version}, ainda não validada por esta fase.`;
}

interface SpecificationSectionProps {
  specification: Specification;
  tools: Discovery['tools'];
  updateSpecification: boolean;
  installSpecification: boolean;
  allowReinstall: boolean;
  onUpdateSpecification: (value: boolean) => void;
  onInstallSpecification: (value: boolean) => void;
  onReinstall: (value: boolean) => void;
}

export function SpecificationSection({
  specification,
  tools,
  updateSpecification,
  installSpecification,
  allowReinstall,
  onUpdateSpecification,
  onInstallSpecification,
  onReinstall
}: SpecificationSectionProps) {
  const missing = missingDependencies(tools);
  const version = versionText(specification);

  return (
    <section aria-labelledby="specification-title" className={rows.section}>
      <h3 id="specification-title" className={rows.title}>
        Estrutura de especificação
      </h3>
      <p className={rows.note}>
        Organiza as especificações do projeto em um ciclo guiado (especificar, desenhar, planejar, executar e validar),
        com foco em qualidade, rastreabilidade e entrega contínua.
      </p>
      {specification.state === 'not_installed' ? (
        <InstallChoice install={installSpecification} onChange={onInstallSpecification} />
      ) : (
        <div className={rows.line}>
          <span className={rows.name}>{specificationStateLabel[specification.state]}</span>
          {version ? <span className={rows.end}>{version}</span> : null}
        </div>
      )}
      <Disclosure
        summary={
          missing.length > 0
            ? `O que a instalação precisa (falta ${missing.join(' e ')})`
            : 'O que a instalação precisa'
        }
      >
        <SpecificationDependencies tools={tools} />
      </Disclosure>
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

/** Não instalar é o padrão: o comando usa a rede, e o motor nunca o executa sem um gesto da pessoa. */
function InstallChoice({ install, onChange }: { install: boolean; onChange: (value: boolean) => void }) {
  const skipId = useId();
  const installId = useId();

  return (
    <fieldset className={panels.fieldset}>
      <legend className="visually-hidden">Instalar a estrutura de especificação agora?</legend>
      <Radio
        name="specification-install"
        checked={!install}
        onChange={() => onChange(false)}
        label="Não instalar agora"
        labelProps={{ className: panels.field }}
        slotProps={{ input: { 'aria-describedby': skipId } }}
      />
      <p id={skipId} className={panels.choiceNote}>
        Sem ela, as verificações que dependem dela ficam bloqueadas no Resultado. Dá para instalar depois, gerando um
        plano novo.
      </p>
      <Radio
        name="specification-install"
        checked={install}
        onChange={() => onChange(true)}
        label="Instalar agora"
        labelProps={{ className: panels.field }}
        slotProps={{ input: { 'aria-describedby': installId } }}
      />
      <p id={installId} className={panels.choiceNote}>
        Executa o instalador oficial, que usa a rede. O comando vem marcado na revisão, onde você confere antes de
        aplicar.
      </p>
    </fieldset>
  );
}
