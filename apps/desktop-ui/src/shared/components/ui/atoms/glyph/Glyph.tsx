import { glyphPaths } from './glyph-paths';
import type { GlyphProps } from './types';

export function Glyph({ name, size = 16, ...rest }: GlyphProps) {
  return (
    <svg
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth={1.5}
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
      focusable="false"
      {...rest}
    >
      {glyphPaths[name]}
    </svg>
  );
}
