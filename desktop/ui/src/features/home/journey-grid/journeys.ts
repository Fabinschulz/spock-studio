export type JourneyId =
  | 'configure-project'
  | 'engineering-request'
  | 'import-requirements'
  | 'connect-tools'
  | 'modernize-system';

export type Journey = {
  id: JourneyId;
  title: string;
  description: string;
  unavailableReason?: string;
};

const SOON = 'Em breve';

export const journeys: readonly Journey[] = [
  {
    id: 'configure-project',
    title: 'Configurar projeto',
    description: 'Analisa o repositório e configura o Harness.'
  },
  {
    id: 'engineering-request',
    title: 'Pedido de engenharia',
    description: 'Analisa objetivo, evidências e impacto sem alterar o repositório.'
  },
  {
    id: 'import-requirements',
    title: 'Importar requisitos',
    description: 'Gere especificações, backlog e roadmap a partir de documentos existentes.',
    unavailableReason: SOON
  },
  {
    id: 'connect-tools',
    title: 'Conectar ferramentas',
    description: 'Configure Jira, Azure DevOps, GitHub, MCPs e bancos de dados.',
    unavailableReason: SOON
  },
  {
    id: 'modernize-system',
    title: 'Modernizar sistema',
    description: 'Analise um legado e planeje sua modernização.',
    unavailableReason: SOON
  }
];
