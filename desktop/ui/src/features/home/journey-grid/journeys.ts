export type JourneyId = 'configure-project' | 'engineering-request' | 'follow-projects' | 'idea' | 'connect-tools';

export type Journey = {
  id: JourneyId;
  title: string;
  description: string;
  unavailableReason?: string;
};

// Só o que existe: uma promessa "em breve" na tela de entrada vale menos que um cartão a menos.
export const journeys: readonly Journey[] = [
  {
    id: 'configure-project',
    title: 'Configurar projeto',
    description: 'Lê a pasta do projeto e prepara as regras e os assistentes de IA dele.'
  },
  {
    id: 'engineering-request',
    title: 'Pedido de engenharia',
    description: 'Descreva o que quer mudar e veja o impacto antes de mexer em qualquer arquivo.'
  },
  {
    id: 'follow-projects',
    title: 'Acompanhar projetos',
    description: 'Veja prazos, riscos e entregas de todos os projetos, sem precisar abrir código.'
  },
  {
    id: 'idea',
    title: 'Da ideia ao projeto',
    description: 'Descreva uma ideia e receba especificação, arquitetura e backlog, conferidos etapa por etapa.'
  },
  {
    id: 'connect-tools',
    title: 'Conectar ferramentas',
    description: 'Conecte GitHub e Jira e decida o que cada projeto pode ler.'
  }
];
