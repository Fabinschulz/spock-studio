export type GlyphState = 'pending' | 'running' | 'done' | 'warning' | 'failed' | 'skipped';

export interface StatusGlyphProps {
  state: GlyphState;
  label?: string;
}
