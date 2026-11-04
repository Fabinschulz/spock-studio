import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { AppProviders } from '@/shared/components';

import { ProposalGuide } from './proposal-guide';

describe('Guia da proposta', () => {
  it('não deixa gerar outro plano enquanto uma reversão ou validação está em andamento', () => {
    const { rerender } = render(
      <AppProviders>
        <ProposalGuide onReplan={() => undefined} replan={{ status: 'idle' }} busy />
      </AppProviders>
    );
    expect(screen.getByRole('button', { name: 'Gerar o plano de novo' })).toBeDisabled();

    rerender(
      <AppProviders>
        <ProposalGuide onReplan={() => undefined} replan={{ status: 'idle' }} busy={false} />
      </AppProviders>
    );
    expect(screen.getByRole('button', { name: 'Gerar o plano de novo' })).toBeEnabled();
  });
});
