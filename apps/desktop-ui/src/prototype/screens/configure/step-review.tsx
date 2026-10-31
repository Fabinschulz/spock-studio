import type { ConfigurationPlan, ConflictResolution } from '@/shared/schema';

import { Glyph } from '../../ui/glyph';
import { Section, Tag } from '../../ui/primitives';
import screen from '../screen.module.scss';

import { StepHeader } from './step-project';

const actionText = {
  create: 'criar',
  update: 'atualizar',
  preserve: 'preservar',
  remove: 'remover',
  conflict: 'conflito'
} as const;
const actionTone = {
  create: 'success',
  update: 'accent',
  preserve: 'neutral',
  remove: 'warning',
  conflict: 'danger'
} as const;
const effectText = {
  process: 'executa um programa',
  network: 'acessa a rede',
  write_local: 'grava arquivos no projeto'
} as const;

interface Props {
  plan: ConfigurationPlan;
  approved: string[];
  commands: string[];
  resolutions: Record<string, ConflictResolution>;
  onToggle: (id: string) => void;
  onToggleCommand: (id: string) => void;
  onResolve: (id: string, value: ConflictResolution) => void;
}

/** Caixa de aprovação: o <input> nativo continua sendo o controle (teclado e leitor de tela); o desenho é só visual. */
function Approve({ checked, label, onChange }: { checked: boolean; label: string; onChange: () => void }) {
  return (
    <label className={screen.approve} data-checked={checked}>
      <span className={screen.check} aria-hidden="true">
        <Glyph name="check" size={12} />
      </span>
      <input className="mz-vh" type="checkbox" checked={checked} onChange={onChange} aria-label={label} />
    </label>
  );
}

export function ReviewStep({ plan, approved, commands, resolutions, onToggle, onToggleCommand, onResolve }: Props) {
  return (
    <>
      <StepHeader
        title="Revisão das alterações"
        lead="Confira o que será criado, atualizado ou removido e aprove o que quiser aplicar. O que você não aprovar não é tocado."
      />

      {plan.commands.length > 0 ? (
        <Section title="Comandos que executam fora dos arquivos" id="comandos" aside="exigem sua aprovação">
          <ul role="list" className={screen.rows}>
            {plan.commands.map((cmd) => (
              <li key={cmd.id} className={screen.row}>
                <Approve
                  checked={commands.includes(cmd.id)}
                  label={`Aprovar: ${cmd.description}`}
                  onChange={() => onToggleCommand(cmd.id)}
                />
                <span>
                  <span className={screen.rowTitle}>{cmd.description}</span>
                  <span className={`${screen.mono} ${screen.block}`}>
                    {cmd.executable} {cmd.arguments.join(' ')}
                  </span>
                  <span className={screen.rowMeta}>{cmd.effects.map((e) => effectText[e]).join(' · ')}</span>
                </span>
                <span />
              </li>
            ))}
          </ul>
        </Section>
      ) : null}

      <Section title={`Arquivos (${plan.changes.length})`} id="arquivos">
        <ul role="list" className={screen.rows}>
          {plan.changes.map((change) => {
            const decidable = change.action === 'create' || change.action === 'update' || change.action === 'remove';
            return (
              <li key={change.id}>
                <div className={screen.row}>
                  {decidable ? (
                    <Approve
                      checked={approved.includes(change.id)}
                      label={`Aprovar ${actionText[change.action]} ${change.path}`}
                      onChange={() => onToggle(change.id)}
                    />
                  ) : (
                    <span className={screen.rowIcon}>
                      <Glyph name={change.action === 'conflict' ? 'alert' : 'file'} size={16} />
                    </span>
                  )}
                  <span className={screen.minZero}>
                    <span className={screen.mono}>{change.path}</span>
                    {change.conflictReason ? (
                      <span className={`${screen.rowMeta} ${screen.block}`}>{change.conflictReason}</span>
                    ) : null}
                  </span>
                  <Tag tone={actionTone[change.action]}>{actionText[change.action]}</Tag>
                </div>
                {change.action === 'conflict' ? (
                  <div className={screen.indent} role="group" aria-label={`Resolução do conflito em ${change.path}`}>
                    <div className={screen.segmented}>
                      <button
                        type="button"
                        aria-pressed={(resolutions[change.id] ?? 'keep') === 'keep'}
                        onClick={() => onResolve(change.id, 'keep')}
                      >
                        Manter o seu arquivo
                      </button>
                      <button
                        type="button"
                        aria-pressed={resolutions[change.id] === 'overwrite'}
                        onClick={() => onResolve(change.id, 'overwrite')}
                      >
                        Substituir (com backup)
                      </button>
                    </div>
                  </div>
                ) : null}
              </li>
            );
          })}
        </ul>
      </Section>

      <Section title="Depois de aplicar, a validação executa" id="validacoes">
        <ul role="list" className={screen.stack}>
          {plan.validations.slice(0, 3).map((v) => (
            <li key={v} className={screen.rowMeta}>
              {v}
            </li>
          ))}
        </ul>
      </Section>
    </>
  );
}
