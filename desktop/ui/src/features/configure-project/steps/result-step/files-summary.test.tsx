import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { FilesSummary } from './files-summary';

const files = (over: Partial<Parameters<typeof FilesSummary>[0]['files']> = {}) => ({
  created: [],
  updated: [],
  removed: [],
  preserved: [],
  kept: [],
  notApplied: [],
  commandEffects: [],
  ...over
});

describe('FilesSummary', () => {
  it('mostra selo só para o grupo que tem arquivo', () => {
    render(<FilesSummary files={files({ created: ['AGENTS.md', '.mcp.json'], preserved: ['README.md'] })} />);

    expect(screen.getByText('2 criados')).toBeTruthy();
    expect(screen.getByText('1 preservado')).toBeTruthy();
    expect(screen.queryByText(/0 removidos/)).toBeNull();
    expect(screen.queryByText(/0 não aplicados/)).toBeNull();
  });

  it('sem arquivo nenhum, diz isso em vez de seis selos zerados', () => {
    render(<FilesSummary files={files()} />);

    expect(screen.getByText('Nenhum arquivo foi alterado.')).toBeTruthy();
    expect(screen.queryByText(/^0 /)).toBeNull();
  });
});
