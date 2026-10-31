export const toggleId = (list: string[], id: string): string[] =>
  list.includes(id) ? list.filter((item) => item !== id) : [...list, id];
