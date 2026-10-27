import { Alert, Button } from '@synthra.io/ui-kit';

import { StatusChip } from '@/shared/components/ui';
import type { Discovery } from '@/shared/schemas';

import type { Loadable } from '../../core/journey-state';
import { aiEnvironmentNames, tlcStateLabel, tlcStateTone } from '../../core/labels';
import styles from '../../ui/panels.module.scss';
import { StepHeader } from '../../ui/step-header';

interface MethodStepProps {
  discovery: Discovery;
  aiEnvironment: string | null;
  allowReinstall: boolean;
  plan: Loadable<unknown>;
  onEnvironment: (id: string) => void;
  onReinstall: (value: boolean) => void;
  onBack: () => void;
  onPlan: () => void;
}

const KNOWN_ENVIRONMENTS = ['claude-code', 'cursor', 'codex'] as const;

export function MethodStep({
  discovery,
  aiEnvironment,
  allowReinstall,
  plan,
  onEnvironment,
  onReinstall,
  onBack,
  onPlan
}: MethodStepProps) {
  const detected = new Map(discovery.aiEnvironments.map((e) => [e.id, e]));
  const selectedUnsupported = aiEnvironment !== null && detected.get(aiEnvironment)?.supported === false;
  const unsupportedSelected = aiEnvironment !== null && aiEnvironment !== 'claude-code';
  const { tlc, tools } = discovery;
  const needsForce = tlc.state === 'drifted' || tlc.state === 'invalid';

  return (
    <section className={styles.step}>
      <StepHeader title="Método e ambiente de IA">
        O método de desenvolvimento orientado a especificações é fixo nesta fase. Falta escolher o ambiente de IA em que
        o Harness será configurado.
      </StepHeader>

      <div className={styles.card}>
        <h3 className={styles.cardTitle}>
          TLC (Tech Leads Club)
          <StatusChip tone={tlcStateTone(tlc.state)}>{tlcStateLabel[tlc.state]}</StatusChip>
        </h3>
        <span className={styles.muted}>
          Método SDD único desta fase: Specify, Design, Tasks, Execute e Validate, com gates por script. Licença da
          skill: CC-BY-4.0, com atribuição ao Tech Leads Club.
        </span>
        {tlc.version ? (
          <span className={styles.muted}>
            Versão {tlc.version} {tlc.versionTested ? '(validada por esta fase)' : '(não validada por esta fase)'}
          </span>
        ) : null}
        <div className={styles.chips}>
          <StatusChip tone={tools.node ? 'success' : 'warning'}>Node {tools.node ?? 'ausente'}</StatusChip>
          <StatusChip tone={tools.python ? 'success' : 'warning'}>Python {tools.python ?? 'ausente'}</StatusChip>
        </div>
        {tlc.state === 'not_installed' ? (
          <span className={styles.muted}>
            O TLC será instalado pelo instalador oficial (@tech-leads-club/agent-skills) somente se você aprovar o
            comando na revisão.
          </span>
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

      <fieldset className={styles.fieldset}>
        <legend>Ambiente de IA do projeto</legend>
        {KNOWN_ENVIRONMENTS.map((id) => {
          const found = detected.get(id);
          return (
            <label key={id} className={styles.field}>
              <input
                type="radio"
                name="ai-environment"
                value={id}
                checked={aiEnvironment === id}
                onChange={() => onEnvironment(id)}
              />
              <span>
                {aiEnvironmentNames[id]}
                <span className={styles.chips}>
                  {found ? <StatusChip tone="info">detectado</StatusChip> : null}
                  <StatusChip tone={id === 'claude-code' ? 'success' : 'warning'}>
                    {id === 'claude-code' ? 'configuração suportada' : 'sem suporte de configuração nesta fase'}
                  </StatusChip>
                </span>
              </span>
            </label>
          );
        })}
      </fieldset>

      {unsupportedSelected || selectedUnsupported ? (
        <Alert
          severity="warning"
          title="Ambiente sem suporte nesta fase"
          description="Só a declaração do projeto (mariliz.yaml) pode ser criada de forma genérica. Agentes, skills, regras e hooks não serão gerados para este ambiente."
        />
      ) : null}

      <div className={styles.card}>
        <h3 className={styles.cardTitle}>O que esta configuração poderá fazer</h3>
        <ul className={styles.list}>
          <li>Ler os arquivos do projeto (já feito, somente leitura).</li>
          <li>Gravar apenas em .claude/, .mariliz/, mariliz.yaml e mariliz.lock, e só o que você aprovar.</li>
          <li>Executar programas ou acessar a rede somente com a aprovação do comando específico.</li>
          <li>Nenhum serviço externo, banco de dados ou integração é configurado nesta jornada.</li>
        </ul>
      </div>

      {plan.status === 'error' ? (
        <Alert severity="error" title="Não foi possível gerar o plano" description={plan.message} />
      ) : null}

      <div className={styles.actions}>
        <Button variant="outlined" color="inherit" onClick={onBack}>
          Voltar
        </Button>
        <Button variant="contained" onClick={onPlan} disabled={aiEnvironment === null || plan.status === 'loading'}>
          {plan.status === 'loading' ? 'Gerando plano…' : 'Gerar plano'}
        </Button>
      </div>
    </section>
  );
}
