import type { SVGProps } from 'react';

export type GlyphName =
  | 'home'
  | 'folder'
  | 'check'
  | 'chevron-right'
  | 'chevron-down'
  | 'panel-right'
  | 'panel-left'
  | 'play'
  | 'pause'
  | 'restart'
  | 'copy'
  | 'alert'
  | 'x'
  | 'minus'
  | 'spark'
  | 'cpu'
  | 'file'
  | 'git'
  | 'layers'
  | 'settings'
  | 'plug'
  | 'clock'
  | 'arrow-right'
  | 'arrow-left';

export interface GlyphProps extends Omit<SVGProps<SVGSVGElement>, 'name'> {
  name: GlyphName;
  size?: number;
}
