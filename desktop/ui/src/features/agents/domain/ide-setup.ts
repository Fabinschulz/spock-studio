export const SERVER_NAME = 'spock';

export interface IdeSetup {
  id: string;
  name: string;
  how: string;
  snippet: string;
}

/**
 * Nome do servidor de uma conexão restrita a um projeto. Outro nome que o global, para os dois conviverem na mesma
 * ferramenta sem um sobrescrever o outro.
 */
export function scopedServerName(project: { projectId: string; displayName: string }): string {
  const slug = project.displayName
    .normalize('NFD')
    .replace(/\p{M}/gu, '')
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '')
    .slice(0, 30)
    .replace(/-+$/g, '');
  return `${SERVER_NAME}-${slug || project.projectId}`;
}

/** O passo de cada ferramenta para registrar o servidor MCP do Spock, a partir do executável do motor. */
export function ideSetups(command: string, args: readonly string[], serverName: string = SERVER_NAME): IdeSetup[] {
  const launch = [`"${command}"`, ...args].join(' ');
  return [
    {
      id: 'claude-code',
      name: 'Claude Code',
      how: 'Rode no terminal. Vale para todos os seus projetos.',
      snippet: `claude mcp add --scope user ${serverName} -- ${launch}`
    },
    {
      id: 'codex',
      name: 'Codex CLI',
      how: 'Rode no terminal. Vale para todos os seus projetos.',
      snippet: `codex mcp add ${serverName} -- ${launch}`
    },
    {
      id: 'cursor',
      name: 'Cursor',
      how: 'Junte ao arquivo mcp.json da pasta .cursor do seu usuário (crie o arquivo se ele não existir).',
      snippet: JSON.stringify({ mcpServers: { [serverName]: { command, args } } }, null, 2)
    },
    {
      id: 'gemini',
      name: 'Gemini CLI',
      how: 'Rode no terminal. Vale para todos os seus projetos.',
      snippet: `gemini mcp add --scope user ${serverName} ${launch}`
    }
  ];
}
