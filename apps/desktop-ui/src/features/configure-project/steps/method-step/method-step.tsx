import { Alert, Button } from '@synthra.io/ui-kit';

import { Checklist, InlineNotice, PrimaryActionFooter, SectionCard, StatusChip } from '@/shared/components/ui';
import type { Discovery } from '@/shared/schemas';

import type { Loadable } from '../../core/journey-state';
import { aiEnvironmentNames, tlcStateLabel, tlcStateTone } from '../../core/labels';
import styles from '../../ui/panels.module.scss';
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
  'Specify, Design, Tasks e Execute, com a profundidade ajustada à complexidade',
  'Requisitos testáveis e tarefas atômicas, com um commit por tarefa',
  'Verificador independente: quem escreve não verifica',
  'Gates por script, aplicados pelo código e não pela memória'
];

function EnvironmentGlyph({ id }: { id: string }) {
  const letter = id === 'claude-code' ? 'C' : id === 'cursor' ? 'Cu' : 'Cx';
  return (
    <span className={styles.envGlyph} aria-hidden="true">
      {letter}
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
          <SectionCard
            className={styles.areaTlc}
            title="TLC (Tech Leads Club)"
            icon={<TlcGlyph size={20} />}
            tone="accent"
            aside={
              <>
                <StatusChip tone="info">Padrão</StatusChip>
                {/* "Não instalado" já aparece no bloco de instalação, logo abaixo: não se repete. */}
                {tlc.state === 'not_installed' ? null : (
                  <StatusChip tone={tlcStateTone(tlc.state)}>{tlcStateLabel[tlc.state]}</StatusChip>
                )}
              </>
            }
          >
            <div className={styles.twoCols}>
              <div className={styles.stack}>
                <span className={styles.muted}>
                  Método SDD único desta fase. Licença da skill: CC-BY-4.0, com atribuição ao Tech Leads Club.
                </span>
                <h4 className={styles.sectionTitle}>Principais recursos</h4>
                <Checklist items={TLC_FEATURES.map((text) => ({ text }))} />
              </div>

              <div className={styles.stack}>
                <h4 className={styles.sectionTitle}>{tlc.version ? 'Versão instalada' : 'Instalação'}</h4>
                <div className={styles.row}>
                  {tlc.version ? <StatusChip tone="info">v{tlc.version}</StatusChip> : null}
                  <StatusChip tone={versionLabel.tone}>{versionLabel.text}</StatusChip>
                </div>
                {tlc.version ? (
                  <span className={styles.muted}>
                    {tlc.versionTested ? 'Versão validada por esta fase.' : 'Versão ainda não validada por esta fase.'}
                  </span>
                ) : null}
                <h4 className={styles.sectionTitle}>Dependências</h4>
                <Checklist
                  label="Dependências do TLC"
                  items={[
                    {
                      text: tools.node ? `Node.js ${tools.node}` : 'Node.js: não encontrado',
                      state: tools.node ? 'ok' : 'warn'
                    },
                    {
                      text: tools.python ? `Python ${tools.python}` : 'Python 3: não encontrado',
                      state: tools.python ? 'ok' : 'warn'
                    },
                    {
                      text: tools.git ? `Git ${tools.git}` : 'Git: não encontrado (recomendado)',
                      state: tools.git ? 'ok' : 'neutral'
                    }
                  ]}
                />
              </div>
            </div>

            {tlc.state === 'not_installed' ? (
              <span className={styles.muted}>
                O TLC será instalado pelo instalador oficial (@tech-leads-club/agent-skills) somente se você aprovar o
                comando na revisão.
              </span>
            ) : null}
            {tlc.state === 'installed' ? (
              <label className={styles.field}>
                <input type="checkbox" checked={updateTlc} onChange={(e) => onUpdateTlc(e.target.checked)} />
                <span>
                  Buscar a versão mais recente do TLC
                  <br />
                  <span className={styles.muted}>
                    O plano já propõe a atualização quando a versão instalada é anterior à mais recente conhecida.
                    Marque para pedir ao instalador oficial a versão mais nova disponível. Usa a rede e só executa se
                    você aprovar o comando.
                  </span>
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
          </SectionCard>

          <SectionCard className={styles.areaEnv} title="Ambiente de IA">
            <fieldset className={styles.fieldset}>
              <legend className="visually-hidden">Ambiente de IA do projeto</legend>
              <p className={styles.muted}>Selecione o ambiente que será utilizado.</p>
              {KNOWN_ENVIRONMENTS.map((id) => {
                const found = detected.get(id);
                return (
                  <label key={id} className={styles.envItem}>
                    <input
                      type="radio"
                      name="ai-environment"
                      value={id}
                      checked={aiEnvironment === id}
                      onChange={() => onEnvironment(id)}
                    />
                    <EnvironmentGlyph id={id} />
                    <span className={styles.envText}>
                      {aiEnvironmentNames[id]}
                      <span className={styles.chips}>
                        {found ? <StatusChip tone="info">detectado</StatusChip> : null}
                        <StatusChip tone={id === 'claude-code' ? 'success' : 'warning'}>
                          {id === 'claude-code' ? 'configuração suportada' : 'sem suporte nesta fase'}
                        </StatusChip>
                      </span>
                    </span>
                  </label>
                );
              })}
              <p className={styles.muted}>
                O ambiente selecionado será usado para configurar os agentes, skills, hooks e demais componentes do
                Harness.
              </p>
            </fieldset>
          </SectionCard>

          <SectionCard className={styles.areaSummary} title="Resumo da configuração">
            <dl className={styles.stack}>
              <div>
                <dt className={styles.sectionTitle}>Método de especificação</dt>
                <dd className={styles.row}>
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
            <p className={styles.muted}>
              Nada é gravado antes da revisão: a configuração só é aplicada depois da sua aprovação.
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
        <Button variant="contained" onClick={onPlan} disabled={aiEnvironment === null || plan.status === 'loading'}>
          {plan.status === 'loading' ? 'Gerando plano…' : 'Gerar plano'}
        </Button>
      </PrimaryActionFooter>
    </section>
  );
}
