const MODIFIER = /Mac|iPhone|iPad/.test(navigator.userAgent) ? '⌘' : 'Ctrl';

export type ShortcutId = 'palette' | 'rail' | 'activity' | 'panel' | 'terminal' | 'shortcuts';

interface Shortcut {
  id: ShortcutId;
  /** Letras pelo caractere, que segue o layout do teclado. */
  key: string;
  /** A crase pela tecla física (à esquerda do 1): no ABNT2 ela é tecla morta e o caractere nem chega. */
  code?: string;
  keyLabel: string;
  description: string;
}

export const SHORTCUTS: readonly Shortcut[] = [
  {
    id: 'palette',
    key: 'k',
    keyLabel: 'K',
    description: 'Buscar uma tela, um projeto ou um comando, ou fazer um pedido'
  },
  { id: 'rail', key: 'b', keyLabel: 'B', description: 'Recolher ou expandir o menu da área' },
  { id: 'activity', key: '.', keyLabel: '.', description: 'Abrir ou fechar o painel de atividade (tarefas e feed)' },
  {
    id: 'panel',
    key: 'j',
    keyLabel: 'J',
    description: 'Abrir ou fechar o painel inferior (fechar também tira o foco do terminal)'
  },
  {
    id: 'terminal',
    key: '`',
    code: 'Backquote',
    keyLabel: '`',
    description: 'Abrir o terminal do projeto (tecla à esquerda do 1)'
  },
  { id: 'shortcuts', key: '/', keyLabel: '/', description: 'Ver os atalhos de teclado' }
];

export function shortcutLabel(id: ShortcutId): string {
  const shortcut = SHORTCUTS.find((entry) => entry.id === id);
  return shortcut ? `${MODIFIER} ${shortcut.keyLabel}` : '';
}

export function shortcutOf(event: Pick<KeyboardEvent, 'ctrlKey' | 'metaKey' | 'altKey' | 'shiftKey' | 'key' | 'code'>) {
  if (!(event.ctrlKey || event.metaKey) || event.altKey || event.shiftKey) return null;
  const key = event.key.toLowerCase();
  return (
    SHORTCUTS.find((entry) => entry.key === key || (entry.code !== undefined && entry.code === event.code))?.id ?? null
  );
}
