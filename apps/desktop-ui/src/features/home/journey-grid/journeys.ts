export type JourneyId = 'configure-project' | 'import-requirements' | 'connect-tools' | 'modernize-system';

export type Journey = {
  id: JourneyId;
  title: string;
  description: string;
  /** Preenchido enquanto a jornada não existe no motor; nenhuma jornada é simulada. */
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
