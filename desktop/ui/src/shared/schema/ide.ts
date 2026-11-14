import { z } from 'zod';

/** Como uma IDE inicia o servidor MCP do Spock: o executável do motor com `args`. `command` some se o motor não souber o próprio caminho. */
export const ideConnectionSchema = z.object({
  command: z.string().nullable(),
  args: z.array(z.string()),
  tools: z.array(z.object({ name: z.string(), title: z.string(), description: z.string() }))
});

export type IdeConnection = z.infer<typeof ideConnectionSchema>;
