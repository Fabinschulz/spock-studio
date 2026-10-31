export const readString = (data: Record<string, unknown>, key: string): string | null =>
  typeof data[key] === 'string' ? data[key] : null;
