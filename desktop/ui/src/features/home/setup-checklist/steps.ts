import { paths } from '@/shared/routing';

export type SetupStepId = 'project' | 'prepare' | 'model' | 'tools';

/** `null` enquanto o motor não respondeu: um passo só aparece como feito quando há confirmação. */
export type SetupStatus = Record<SetupStepId, boolean | null>;

export interface SetupStep {
  id: SetupStepId;
  title: string;
  /** Por que vale a pena, em linguagem de quem ainda não conhece o produto. */
  description: string;
  action: string;
  to: string;
  optional?: boolean;
}

export const SETUP_STEPS: readonly SetupStep[] = [
  {
    id: 'project',
    title: 'Abrir um projeto',
    description:
      'O Spock Studio lê a pasta para identificar as tecnologias. A leitura não altera nada e os arquivos permanecem no computador.',
    action: 'Selecionar pasta',
    to: paths.configure
  },
  {
    id: 'prepare',
    title: 'Preparar o projeto para a IA',
    description:
      'Gera as regras, as verificações e os assistentes de IA do projeto. Você revisa cada arquivo antes de gravar e pode desfazer depois.',
    action: 'Configurar',
    to: paths.configure
  },
  {
    id: 'model',
    title: 'Conectar um modelo de IA',
    description:
      'Com ele, o Spock Studio explica análises e responde perguntas. Sem ele, as leituras e verificações locais continuam funcionando.',
    action: 'Conectar modelo',
    to: paths.connectionsModels,
    optional: true
  },
  {
    id: 'tools',
    title: 'Conectar ferramentas',
    description:
      'GitHub e Jira trazem prazos, riscos e entregas para a Gestão. Você decide o que cada projeto pode ler.',
    action: 'Conectar',
    to: paths.integrations,
    optional: true
  }
];

export function countDone(status: SetupStatus): number {
  return SETUP_STEPS.filter((step) => status[step.id] === true).length;
}
