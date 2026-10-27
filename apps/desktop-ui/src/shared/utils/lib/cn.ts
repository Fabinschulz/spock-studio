import { clsx, type ClassValue } from 'clsx';

/** Junta classes condicionalmente (padrão do Mariliz Web, só com `clsx`). */
export function cn(...inputs: ClassValue[]): string {
  return clsx(inputs);
}
