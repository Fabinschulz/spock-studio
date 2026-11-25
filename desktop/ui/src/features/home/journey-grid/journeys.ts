import { paths } from '@/shared/routing';

export type JourneyId =
  | 'open-project'
  | 'project-overview'
  | 'configure-project'
  | 'follow-delivery'
  | 'follow-projects'
  | 'idea'
  | 'connect-tools';

export interface Journey {
  id: JourneyId;
  title: string;
  description: string;
  to: string;
  /** A tela de destino trabalha sobre uma pasta: sem projeto aberto, a pessoa a escolhe antes de seguir. */
  needsProject: boolean;
  primary?: boolean;
}

export interface JourneyContext {
  hasProject: boolean;
  /** Já abriu algum projeto neste computador. */
  hasRecents: boolean;
}

const FOLLOW_PROJECTS: Journey = {
  id: 'follow-projects',
  title: 'Consultar meus projetos',
  description: 'Veja prazos, riscos e entregas de todos os projetos, sem precisar abrir código.',
  to: paths.portfolio,
  needsProject: false
};

const CONNECT_TOOLS: Journey = {
  id: 'connect-tools',
  title: 'Conectar ferramentas',
  description: 'Conecte GitHub e Jira e decida o que cada projeto pode ler.',
  to: paths.integrations,
  needsProject: false
};

/**
 * Só o que existe e só o que serve ao momento: quem ainda não tem projeto recebe o caminho para começar; quem já tem,
 * os atalhos para continuar. Uma promessa "em breve" na tela de entrada vale menos que um cartão a menos.
 */
export function journeysFor({ hasProject, hasRecents }: JourneyContext): readonly Journey[] {
  if (!hasProject)
    return [
      {
        id: 'open-project',
        title: hasRecents ? 'Abrir um projeto' : 'Conectar meu primeiro projeto',
        description:
          'Escolha a pasta para o Spock Studio ler a estrutura, identificar as tecnologias e preparar o ambiente. Os arquivos permanecem no computador.',
        to: paths.configure,
        needsProject: true,
        primary: true
      },
      FOLLOW_PROJECTS,
      CONNECT_TOOLS
    ];

  return [
    {
      id: 'project-overview',
      title: 'Analisar o projeto',
      description: 'Veja o que foi encontrado: tecnologias, padrões aplicados e o que já foi executado.',
      to: paths.project,
      needsProject: true,
      primary: true
    },
    {
      id: 'configure-project',
      title: 'Configurar meu ambiente',
      description: 'Lê a pasta do projeto e prepara as regras e os assistentes de IA dele.',
      to: paths.configure,
      needsProject: true
    },
    {
      id: 'follow-delivery',
      title: 'Acompanhar minhas entregas',
      description: 'O andamento do projeto aberto: atividades, riscos, marcos e decisões.',
      to: paths.management,
      needsProject: true
    },
    {
      id: 'idea',
      title: 'Da ideia ao projeto',
      description: 'Descreva uma ideia e receba especificação, arquitetura e backlog, conferidos etapa por etapa.',
      to: paths.idea,
      needsProject: true
    },
    FOLLOW_PROJECTS,
    CONNECT_TOOLS
  ];
}
