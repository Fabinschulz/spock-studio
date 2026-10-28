import { Alert, ArrowRightIcon, Button } from '@synthra.io/ui-kit';

import { Checklist, InlineNotice, PrimaryActionFooter, SectionCard, StatusChip } from '@/shared/components/ui';
import type { Discovery } from '@/shared/schemas';

import type { Loadable } from '../../core/journey-state';
import { aiEnvironmentNames, tlcStateLabel, tlcStateTone } from '../../core/labels';
import styles from '../../ui/panels.module.scss';
import { GitGlyph, NodeGlyph, NodesGlyph, PythonGlyph } from '../../ui/glyphs';
import { TlcGlyph } from '../../ui/method-badge';
import { StepHeader } from '../../ui/step-header';

interface MethodStepProps {
  discovery: Discovery;
  aiEnvironment: string | null;
  allowReinstall: boolean;
  updateTlc: boolean;
  plan: Loadable<unknown>;
  onEnvironment: (id: string) => void;
  onReinstall: (value: boolean) => void;
  onUpdateTlc: (value: boolean) => void;
  onBack: () => void;
  onPlan: () => void;
}

const KNOWN_ENVIRONMENTS = ['claude-code', 'cursor', 'codex'] as const;

/** O que o TLC entrega, segundo a descrição da própria skill. */
const TLC_FEATURES = [
  'Especificações estruturadas',
  'Planejamento e execução guiada',
  'Integração com agentes e ferramentas',
  'Modelo de documentação e validação'
];

/**
 * Marcas dos ambientes, redesenhadas em traço simples (a Synthra não traz esses logotipos e os arquivos oficiais não
 * fazem parte do projeto): Claude (raios), Cursor (cubo com seta) e OpenAI Codex (nó hexagonal). Decorativas.
 */
function EnvironmentGlyph({ id }: { id: string }) {
  const common = {
    width: 20,
    height: 20,
    viewBox: '0 0 24 24',
    fill: 'none',
    'aria-hidden': true,
    focusable: false
  } as const;
  const line = { stroke: 'currentColor', strokeWidth: 1.7, strokeLinecap: 'round', strokeLinejoin: 'round' } as const;
  return (
    <span className={styles.envGlyph} aria-hidden="true">
      {id === 'claude-code' ? (
        <svg {...common}>
          <g {...line}>
            <path d="M12 3v18M3 12h18M5.6 5.6l12.8 12.8M18.4 5.6 5.6 18.4" />
          </g>
        </svg>
      ) : id === 'cursor' ? (
        <svg {...common}>
          <g {...line}>
            <path d="M12 2.8 20 7.4v9.2l-8 4.6-8-4.6V7.4l8-4.6Z" />
            <path d="m9 9 7 2.6-3 1.4-1.4 3L9 9Z" />
          </g>
        </svg>
      ) : (
        <svg {...common}>
          <g {...line}>
            <path d="M12 3.2 19 7.2v8L12 20.8 5 16.8v-8l7-4Z" />
            <path d="M12 3.2v8.2l7 4M12 11.4l-7 4M5 7.2l7 4.2 7-4.2" />
          </g>
        </svg>
      )}
    </span>
  );
}

export function MethodStep({
  discovery,
  aiEnvironment,
  allowReinstall,
  updateTlc,
  plan,
  onEnvironment,
  onReinstall,
  onUpdateTlc,
  onBack,
  onPlan
}: MethodStepProps) {
  const detected = new Map(discovery.aiEnvironments.map((e) => [e.id, e]));
  const selectedUnsupported = aiEnvironment !== null && detected.get(aiEnvironment)?.supported === false;
  const unsupportedSelected = aiEnvironment !== null && aiEnvironment !== 'claude-code';
  const { tlc, tools } = discovery;
  const needsForce = tlc.state === 'drifted' || tlc.state === 'invalid';

  const versionLabel = tlc.version
    ? tlc.versionTested
      ? { text: 'Compatível', tone: 'success' as const }
      : { text: 'Não validada', tone: 'warning' as const }
    : { text: 'Não instalado', tone: 'neutral' as const };

  return (
    <section className={styles.step}>
      <StepHeader title="Método de especificação e ambiente">
        O TLC estrutura as especificações do seu projeto, e esse método é fixo nesta fase. O Harness Engineer planeja a
        configuração das demais estruturas no ambiente de IA que você escolher.
      </StepHeader>

      <div className={styles.methodWrap}>
        <div className={styles.methodGrid}>
          <section className={`${styles.areaTlc} ${styles.tlcCard}`} aria-labelledby="tlc-titulo">
            <div className={styles.tlcGrid}>
              <span className={styles.tlcIcon} aria-hidden="true">
                <TlcGlyph size={28} />
              </span>
              <div className={styles.stack}>
                <header className={styles.tlcHeader}>
                  <h3 id="tlc-titulo" className={styles.tlcTitle}>
                    TLC (Tech Leads Club)
                  </h3>
                  <StatusChip tone="info">Padrão</StatusChip>
                  {/* "Não instalado" já aparece no bloco de instalação, ao lado: não se repete. */}
                  {tlc.state === 'not_installed' ? null : (
                    <StatusChip tone={tlcStateTone(tlc.state)}>{tlcStateLabel[tlc.state]}</StatusChip>
                  )}
                </header>
                <span className={styles.tlcDescription}>
                  Método de especificação e desenvolvimento orientado a boas práticas, com foco em qualidade,
                  rastreabilidade e entrega contínua.
                </span>
                <div className={styles.featureGroup}>
                  <h4 className={styles.featuresTitle}>Principais recursos</h4>
                  <Checklist items={TLC_FEATURES.map((text) => ({ text }))} />
                </div>
              </div>

              <div className={styles.stack}>
                <h4 className={styles.fieldLabel}>{tlc.version ? 'Versão instalada' : 'Instalação'}</h4>
                <div className={styles.row}>
                  {tlc.version ? <span className={styles.version}>v{tlc.version}</span> : null}
                  <StatusChip tone={versionLabel.tone}>{versionLabel.text}</StatusChip>
                </div>
                {tlc.version ? (
                  <span className={styles.muted}>
                    {tlc.versionTested ? 'Versão validada para esta fase.' : 'Versão ainda não validada por esta fase.'}
                  </span>
                ) : null}
                <h4 className={styles.fieldLabel}>Dependências</h4>
                <ul className={styles.deps} aria-label="Dependências do TLC">
                  {[
                    {
                      id: 'node',
                      icon: <NodeGlyph />,
                      text: tools.node ? `Node.js ${tools.node}` : 'Node.js: não encontrado',
                      ok: !!tools.node
                    },
                    {
                      id: 'python',
                      icon: <PythonGlyph />,
                      text: tools.python ? `Python ${tools.python}` : 'Python 3: não encontrado',
                      ok: !!tools.python
                    },
                    {
                      id: 'git',
                      icon: <GitGlyph />,
                      text: tools.git ? `Git ${tools.git}` : 'Git: não encontrado (recomendado)',
                      ok: !!tools.git
                    }
                  ].map((dep) => (
                    <li key={dep.id} className={dep.ok ? styles.dep : `${styles.dep} ${styles.depMissing}`}>
                      <span className={styles.depIcon} aria-hidden="true">
                        {dep.icon}
                      </span>
                      {dep.text}
                    </li>
                  ))}
                </ul>
              </div>

              <div className={styles.tlcFooter}>
                {tlc.state === 'not_installed' ? (
                  <span className={styles.muted}>
                    O TLC será instalado pelo instalador oficial (@tech-leads-club/agent-skills) somente se você aprovar
                    o comando na revisão.
                  </span>
                ) : null}
                {tlc.state === 'installed' ? (
                  <label className={styles.subCard}>
                    <input type="checkbox" checked={updateTlc} onChange={(e) => onUpdateTlc(e.target.checked)} />
                    <span className={styles.subCardIcon} aria-hidden="true">
                      <svg width="20" height="20" viewBox="0 0 20 20" fill="none" focusable="false">
                        <path
                          d="M16.5 10a6.5 6.5 0 0 1-11.1 4.6M3.5 10a6.5 6.5 0 0 1 11.1-4.6M15 2.8v3.2h-3.2M5 17.2V14h3.2"
                          stroke="currentColor"
                          strokeWidth="1.6"
                          strokeLinecap="round"
                          strokeLinejoin="round"
                        />
                      </svg>
                    </span>
                    <strong className={styles.subCardTitle}>Buscar a versão mais recente do TLC</strong>
                    <span className={styles.subCardText}>
                      O plano já propõe a atualização quando a versão instalada é anterior à mais recente conhecida.
                      Marque para pedir ao instalador oficial a versão mais nova disponível. Usa a rede e só executa se
                      você aprovar o comando.
                    </span>
                  </label>
                ) : null}
                {needsForce ? (
                  <label className={styles.field}>
                    <input type="checkbox" checked={allowReinstall} onChange={(e) => onReinstall(e.target.checked)} />
                    <span>
                      Permitir reinstalar o TLC
                      <br />
                      <span className={styles.muted}>
                        Sobrescreve as edições locais da skill. Sem esta permissão, o TLC não é alterado.
                      </span>
                    </span>
                  </label>
                ) : null}
              </div>
            </div>
          </section>

          <SectionCard className={`${styles.areaEnv} ${styles.fillCard}`} title="Ambiente de IA" icon={<NodesGlyph />}>
            <fieldset className={styles.fieldset}>
              <legend className="visually-hidden">Ambiente de IA do projeto</legend>
              <p className={styles.muted}>Selecione o ambiente que será utilizado.</p>
              {KNOWN_ENVIRONMENTS.map((id) => {
                const found = detected.get(id);
                return (
                  <label key={id} className={`${styles.envItem} ${id === 'claude-code' ? '' : styles.envMuted}`}>
                    <input
                      type="radio"
                      name="ai-environment"
                      value={id}
                      checked={aiEnvironment === id}
                      onChange={() => onEnvironment(id)}
                    />
                    <EnvironmentGlyph id={id} />
                    <span className={styles.envName}>{aiEnvironmentNames[id]}</span>
                    {found ? <span className={styles.envTag}>detectado</span> : null}
                    {id === 'claude-code' ? (
                      <span className="visually-hidden">configuração suportada</span>
                    ) : (
                      <span className={`${styles.envTag} ${styles.envTagWarn}`}>
                        sem suporte<span className="visually-hidden"> nesta fase</span>
                      </span>
                    )}
                  </label>
                );
              })}
              <p className={styles.cardNote}>
                <svg width="16" height="16" viewBox="0 0 16 16" fill="none" aria-hidden="true" focusable="false">
                  <rect x="3" y="7" width="10" height="7" rx="1.5" stroke="currentColor" strokeWidth="1.4" />
                  <path d="M5.5 7V5a2.5 2.5 0 0 1 5 0v2" stroke="currentColor" strokeWidth="1.4" />
                </svg>
                <span>
                  O ambiente selecionado será usado para configurar os agentes, skills, hooks e demais componentes do
                  Harness.
                </span>
              </p>
            </fieldset>
          </SectionCard>

          <SectionCard className={`${styles.areaSummary} ${styles.fillCard}`} title="Resumo da configuração">
            <dl className={styles.stack}>
              <div>
                <dt className={styles.sectionTitle}>Método de especificação</dt>
                <dd className={`${styles.row} ${styles.spread}`}>
                  <span>TLC (padrão)</span>
                  {tlc.version ? <StatusChip tone="info">v{tlc.version}</StatusChip> : null}
                </dd>
              </div>
              <div>
                <dt className={styles.sectionTitle}>Ambiente de IA</dt>
                <dd>{aiEnvironment ? (aiEnvironmentNames[aiEnvironment] ?? aiEnvironment) : 'Ainda não escolhido'}</dd>
              </div>
              <div>
                <dt className={styles.sectionTitle}>O que será configurado</dt>
                <dd>
                  <Checklist
                    items={[
                      { text: 'Agentes: Harness Engineer, revisor de código e verificador' },
                      { text: 'Skill: contrato de evidência' },
                      { text: 'Hook: política de segurança antes de escritas e comandos' },
                      { text: 'Regras gerais, da stack e dos comandos do repositório' },
                      { text: 'Evals: verificações da configuração aplicada' },
                      { text: 'Tools e MCPs: nenhum é instalado nesta jornada', state: 'neutral' }
                    ]}
                  />
                </dd>
              </div>
            </dl>
            <p className={styles.cardNote}>
              <svg width="16" height="16" viewBox="0 0 20 20" fill="none" aria-hidden="true" focusable="false">
                <circle cx="10" cy="10" r="8" stroke="currentColor" strokeWidth="1.6" />
                <path d="M10 9v5M10 6.2v.1" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" />
              </svg>
              <span>Nada é gravado antes da revisão: a configuração só é aplicada depois da sua aprovação.</span>
            </p>
          </SectionCard>
        </div>
      </div>

      {unsupportedSelected || selectedUnsupported ? (
        <InlineNotice tone="warning" title="Ambiente sem suporte nesta fase">
          Só a declaração do projeto (mariliz.yaml) pode ser criada de forma genérica. Agentes, skills, regras e hooks
          não serão gerados para este ambiente.
        </InlineNotice>
      ) : null}

      {plan.status === 'error' ? (
        <Alert severity="error" title="Não foi possível gerar o plano" description={plan.message} />
      ) : null}

      <PrimaryActionFooter>
        <Button variant="outlined" color="inherit" onClick={onBack}>
          Voltar
        </Button>
        <Button
          variant="contained"
          onClick={onPlan}
          disabled={aiEnvironment === null || plan.status === 'loading'}
          endIcon={<ArrowRightIcon />}
        >
          {plan.status === 'loading' ? 'Gerando plano…' : 'Gerar plano'}
        </Button>
      </PrimaryActionFooter>
    </section>
  );
}
