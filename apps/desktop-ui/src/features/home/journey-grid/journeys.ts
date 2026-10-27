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
    description: 'Analisar o repositório e configurar o Harness.',
    unavailableReason: SOON
  },
  {
    id: 'import-requirements',
    title: 'Importar requisitos',
    description: 'Gerar especificações, backlog e roadmap a partir de documentos ou ferramentas existentes.',
    unavailableReason: SOON
  },
  {
    id: 'connect-tools',
    title: 'Conectar ferramentas',
    description: 'Configurar Jira, Azure DevOps, GitHub, MCPs e bancos de dados.',
    unavailableReason: SOON
  },
  {
    id: 'modernize-system',
    title: 'Modernizar sistema',
    description: 'Analisar um legado e planejar sua modernização.',
    unavailableReason: SOON
  }
];
