import type { ITheme } from '@xterm/xterm';

function token(styles: CSSStyleDeclaration, name: string, fallback: string): string {
  return styles.getPropertyValue(name).trim() || fallback;
}

/**
 * Fundo, texto, cursor e as cores de estado saem dos tokens do aplicativo, para o terminal ser parte da tela e não uma
 * janela estranha dentro dela. O xterm ainda garante contraste mínimo (`minimumContrastRatio`) para cores de programas.
 */
export function terminalTheme(root: Element = document.documentElement): ITheme {
  const styles = getComputedStyle(root);
  const background = token(styles, '--color-sidebar', '#101010');
  return {
    background,
    foreground: token(styles, '--color-text', '#ececec'),
    cursor: token(styles, '--color-primary', '#33c1ba'),
    cursorAccent: background,
    selectionBackground: 'rgba(51, 193, 186, 0.3)',
    black: '#2c2c2c',
    red: token(styles, '--tone-danger-fg', '#f2766e'),
    green: token(styles, '--tone-success-fg', '#7bd88f'),
    yellow: token(styles, '--tone-warning-fg', '#e9b44c'),
    blue: token(styles, '--tone-info-fg', '#8cc8f0'),
    magenta: '#d39bef',
    cyan: token(styles, '--color-primary-text', '#4fd3cc'),
    white: '#d4d4d4',
    brightBlack: '#8a8a8a',
    brightRed: '#ff9a92',
    brightGreen: '#9ce8ab',
    brightYellow: '#f5cd78',
    brightBlue: '#b0dcf8',
    brightMagenta: '#e6bdf8',
    brightCyan: '#7fe4de',
    brightWhite: '#ffffff'
  };
}
