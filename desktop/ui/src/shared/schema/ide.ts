import { z } from 'zod';

/** Como uma IDE inicia o servidor MCP do Spock: o executável do motor com `args`. `command` some se o motor não souber o próprio caminho. */
export const ideConnectionSchema = z.object({
  command: z.string().nullable(),
  args: z.array(z.string()),
  /** Repetido por projeto depois de `args`: a conexão passa a ler só esses. Ausente em motor anterior ao escopo. */
  projectArgument: z.string().optional(),
  /** O lançador spock (DD-6): quando existe, a IDE chama ele no lugar de `command`, que é o caminho do motor nesta máquina. */
  launcher: z.string().nullable().optional(),
  tools: z.array(z.object({ name: z.string(), title: z.string(), description: z.string() }))
});

export type IdeConnection = z.infer<typeof ideConnectionSchema>;
