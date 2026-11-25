import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { ActivitySection } from './activity-section';

describe('ActivitySection', () => {
  it('os acontecimentos ficam recolhidos: repetem a lista de etapas da página', () => {
    const { container } = render(
      <ActivitySection
        startedAt={0}
        feed={[
          { at: 1000, text: 'Iniciou: Gravar arquivos' },
          { at: 2000, text: 'Concluiu: Gravar arquivos' }
        ]}
      />
    );

    expect(screen.getByText('2 acontecimentos recentes')).toBeInTheDocument();
    expect(container.querySelector('details')!.open).toBe(false);
    expect(screen.getByText('Concluiu: Gravar arquivos')).toBeInTheDocument();
  });

  it('sem nada ainda, diz isso em texto', () => {
    render(<ActivitySection startedAt={0} feed={[]} />);
    expect(screen.getByText('Nenhum evento ainda.')).toBeInTheDocument();
  });
});
