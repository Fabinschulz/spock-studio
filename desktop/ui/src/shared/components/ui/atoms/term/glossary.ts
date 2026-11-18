// Uma explicação por termo, em português simples, para que a mesma palavra queira dizer a mesma coisa em toda tela.
export const GLOSSARY = {
  harness: {
    label: 'Harness',
    text: 'O conjunto de regras, assistentes de IA e verificações que o Spock Studio instala no projeto para a IA trabalhar do jeito da equipe.'
  },
  agent: {
    label: 'agente',
    text: 'Um assistente de IA com uma função definida, como revisar código, que trabalha dentro do projeto.'
  },
  skill: {
    label: 'skill',
    text: 'Uma instrução pronta que ensina um agente a fazer uma tarefa específica.'
  },
  hook: {
    label: 'hook',
    text: 'Uma checagem automática que roda antes de a IA agir, para permitir, bloquear ou pedir sua confirmação.'
  },
  eval: {
    label: 'eval',
    text: 'Um teste automático que confere se a configuração ficou certa depois de aplicada.'
  },
  mcp: {
    label: 'MCP',
    text: 'Uma conexão que dá à IA acesso a uma ferramenta externa, como um banco de dados ou um serviço.'
  },
  engine: {
    label: 'motor',
    text: 'A parte do Spock Studio que roda neste computador e faz o trabalho; a tela só mostra o que ele faz.'
  },
  localIntelligence: {
    label: 'inteligência local',
    text: 'O componente deste computador que organiza e busca o conteúdo do projeto, sem enviar nada para fora.'
  },
  index: {
    label: 'índice',
    text: 'Uma cópia organizada do projeto que permite à IA achar rápido os trechos que importam para cada pedido.'
  },
  provider: {
    label: 'provedor de IA',
    text: 'A empresa cujo modelo de IA responde, como Anthropic, OpenAI ou Google.'
  },
  model: {
    label: 'modelo de IA',
    text: 'O "cérebro" de IA que escreve as respostas. Modelos maiores costumam acertar mais, mas custam mais.'
  },
  apiKey: {
    label: 'chave de API',
    text: 'Uma senha que o provedor de IA fornece para o Spock Studio usar a sua conta. Cada uso pode ser cobrado pelo provedor.'
  },
  token: {
    label: 'tokens',
    text: 'A unidade que mede o texto enviado e recebido da IA (cerca de ¾ de uma palavra). Define o custo e o limite de cada uso.'
  },
  run: {
    label: 'execução',
    text: 'Cada vez que o Spock Studio fez algo: aplicar, validar, reverter, responder ou analisar um pedido.'
  },
  aiEnvironment: {
    label: 'ambiente de IA',
    text: 'A ferramenta de IA em que você programa, como Claude Code, Cursor ou Codex. Um projeto pode ter mais de uma.'
  },
  revert: {
    label: 'reverter',
    text: 'Desfazer o que uma aplicação mudou, voltando os arquivos ao estado anterior.'
  },
  simulation: {
    label: 'simulações',
    text: 'Futuros possíveis sorteados a partir das entregas reais de cada semana. A data é a que a maioria deles alcança; não é promessa.'
  },
  state: {
    label: 'state',
    text: 'O arquivo em que a ferramenta de infraestrutura anota o que já existe na nuvem, para saber o que criar, mudar ou apagar.'
  },
  drift: {
    label: 'drift',
    text: 'Diferença entre o que está na nuvem e o que o projeto descreve, causada por mudança feita fora do projeto.'
  }
} as const;

export type GlossaryId = keyof typeof GLOSSARY;
