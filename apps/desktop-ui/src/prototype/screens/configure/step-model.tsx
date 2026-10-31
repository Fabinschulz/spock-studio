import { discovery, plan } from '../../data/fixtures';
import { Glyph } from '../../ui/glyph';
import { Section, StatusGlyph, Tag } from '../../ui/primitives';
import screen from '../screen.module.scss';

import { StepHeader } from './step-project';

interface Props {
  env: string | null;
  onEnv: (id: string) => void;
  planning: boolean;
}

const effects = {
  process: 'executa um programa',
  network: 'acessa a rede',
  write_local: 'grava arquivos no projeto'
} as const;

/** Rótulos dos ambientes: o motor devolve só o id e o nome; "sem suporte" vem de `supported`. */
export function ModelStep({ env, onEnv, planning }: Props) {
  const install = plan.commands[0];

  return (
    <>
      <StepHeader
        title="Modelo e ambiente"
        lead="O modelo de implementação Mariliz organiza a engenharia do projeto em agentes, skills, regras, hooks e validações. Escolha o ambiente de IA em que você trabalha."
      />

      <Section title="Estrutura de especificação" id="estrutura">
        <div className={screen.notice}>
          <Glyph name="layers" size={16} />
          <div>
            <p>
              <strong>Será instalada pelo instalador oficial.</strong>
            </p>
            <p>
              {install
                ? `Esta ação ${install.effects.map((e) => effects[e]).join(', ')}. Ela só roda se você aprovar na revisão.`
                : 'Nenhuma instalação é necessária.'}
            </p>
          </div>
        </div>
      </Section>

      <Section title="Ambiente de IA" id="ambiente">
        <div role="radiogroup" aria-label="Ambiente de IA" className={screen.stack}>
          {[
            { id: 'claude-code', name: 'Claude Code', supported: true },
            { id: 'cursor', name: 'Cursor', supported: false },
            { id: 'codex', name: 'OpenAI Codex', supported: false }
          ].map((option) => {
            const checked = env === option.id;
            return (
              <label key={option.id} className={screen.choice} data-checked={checked} aria-disabled={!option.supported}>
                <input
                  type="radio"
                  name="ambiente"
                  value={option.id}
                  checked={checked}
                  disabled={!option.supported || planning}
                  onChange={() => onEnv(option.id)}
                />
                <span className={screen.choiceBody}>
                  <span className={screen.rowTitle}>{option.name}</span>
                  {!option.supported ? <span className={screen.rowMeta}> — sem suporte nesta fase</span> : null}
                </span>
                {option.supported && discovery.aiEnvironments.some((e) => e.id === option.id) ? (
                  <Tag tone="accent">detectado</Tag>
                ) : null}
                <span className={screen.radio} aria-hidden="true" />
              </label>
            );
          })}
        </div>
      </Section>

      {planning ? (
        <div className={screen.working} role="status">
          <div className={screen.workingLine}>
            <StatusGlyph state="running" />
            <span>Montando o plano…</span>
          </div>
          <div className={screen.indeterminate} aria-hidden="true" />
          <p className={screen.note}>
            O motor monta o plano numa única chamada e não informa o andamento. Nada é alterado no projeto.
          </p>
        </div>
      ) : null}
    </>
  );
}
