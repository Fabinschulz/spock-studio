// O armazenamento do navegador pode faltar ou recusar (janela privada, dados bloqueados): as preferências do shell são
// só conforto e, sem ele, duram até fechar o aplicativo.

export function readPreference(key: string): string | null {
  try {
    return window.localStorage.getItem(key);
  } catch {
    return null;
  }
}

export function writePreference(key: string, value: string): void {
  try {
    window.localStorage.setItem(key, value);
  } catch {
    // Sem armazenamento a preferência fica só na memória.
  }
}
