/** Contagem com o substantivo concordando: "1 arquivo", "3 arquivos", nunca "arquivo(s)". */
export const plural = (count: number, one: string, many: string): string => `${count} ${count === 1 ? one : many}`;
