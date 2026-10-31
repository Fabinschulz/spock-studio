import type { GlyphName } from '@/shared/components/ui';

export interface WorkbenchContext {
  wide: boolean;
  panelOpen: boolean;
  setPanelOpen: (open: boolean) => void;
}

export interface NavItem {
  label: string;
  glyph: GlyphName;
  to?: string;
  end?: boolean;
}
