import { EvidenceDisclosure, InlineNotice } from '@/shared/components/ui';
import type { Discovery } from '@/shared/schema';

import { journeySteps } from '../../constants/journey-steps';

const NOT_A_GIT_REPOSITORY = 'NOT_A_GIT_REPOSITORY';
const NO_AI_ENVIRONMENT_DETECTED = 'NO_AI_ENVIRONMENT_DETECTED';

const environmentStep = journeySteps.find((step) => step.id === 'model')!.label;

/**
 * O texto do motor ("selecione o que o projeto usa") serve a qualquer cliente; nesta jornada a escolha só existe na
 * etapa seguinte, então a tela diz onde ela fica em vez de pedir algo que aqui não dá para fazer.
 */
function messageOf(warning: Discovery['warnings'][number]): string {
  return warning.code === NO_AI_ENVIRONMENT_DETECTED
    ? `Nenhum ambiente de IA foi detectado no projeto. Você escolhe qual a equipe usa na próxima etapa, ${environmentStep}.`
    : warning.message;
}

export function DiscoveryWarnings({ warnings }: { warnings: Discovery['warnings'] }) {
  return warnings.map((warning) => {
    const informative = warning.code === NOT_A_GIT_REPOSITORY || warning.code === NO_AI_ENVIRONMENT_DETECTED;

    return (
      <InlineNotice key={warning.code + warning.message} tone={informative ? 'info' : 'warning'}>
        <span>{messageOf(warning)}</span>
        {warning.code === NOT_A_GIT_REPOSITORY ? (
          <span>
            {' '}
            A reversão continua possível pelo backup que a aplicação guarda antes de alterar qualquer arquivo.
          </span>
        ) : null}
        {warning.paths.length > 0 ? (
          <EvidenceDisclosure summary={`Arquivos (${warning.paths.length})`} items={warning.paths} />
        ) : null}
      </InlineNotice>
    );
  });
}
