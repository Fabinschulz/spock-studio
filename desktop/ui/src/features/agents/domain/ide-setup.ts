export const SERVER_NAME = 'spock';

export interface IdeSetup {
  id: string;
  name: string;
  how: string;
  snippet: string;
}

/** O passo de cada ferramenta para registrar o servidor MCP do Spock, a partir do executável do motor. */
export function ideSetups(command: string, args: readonly string[]): IdeSetup[] {
  const launch = [`"${command}"`, ...args].join(' ');
  return [
    {
      id: 'claude-code',
      name: 'Claude Code',
      how: 'Rode no terminal. Vale para todos os seus projetos.',
      snippet: `claude mcp add --scope user ${SERVER_NAME} -- ${launch}`
    },
    {
      id: 'codex',
      name: 'Codex CLI',
      how: 'Rode no terminal. Vale para todos os seus projetos.',
      snippet: `codex mcp add ${SERVER_NAME} -- ${launch}`
    },
    {
      id: 'cursor',
      name: 'Cursor',
      how: 'Junte ao arquivo mcp.json da pasta .cursor do seu usuário (crie o arquivo se ele não existir).',
      snippet: JSON.stringify({ mcpServers: { [SERVER_NAME]: { command, args } } }, null, 2)
    },
    {
      id: 'gemini',
      name: 'Gemini CLI',
      how: 'Rode no terminal. Vale para todos os seus projetos.',
      snippet: `gemini mcp add --scope user ${SERVER_NAME} ${launch}`
    }
  ];
}
