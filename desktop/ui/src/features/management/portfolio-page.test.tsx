import { render, screen, within } from '@testing-library/react';
import { MemoryRouter } from 'react-router';
import { describe, expect, it } from 'vitest';

import { AppProviders } from '@/shared/components';
import { expectNoA11yViolations } from '@/test/a11y';
import { fakeIntegrationEngine } from '@/test/fake-integration-engine';

import { PortfolioPage } from './portfolio-page';

describe('Gestão · portfólio', () => {
  it('mostra cada projeto com os próprios números e quem pede atenção primeiro', async () => {
    const engine = fakeIntegrationEngine();
    const { container } = render(
      <AppProviders>
        <MemoryRouter>
          <PortfolioPage client={engine.client} />
        </MemoryRouter>
      </AppProviders>
    );

    const list = await screen.findByRole('list', { name: 'Projetos' });
    const [portal, loja] = within(list)
      .getAllByRole('listitem', { name: '' })
      .filter((item) => item.parentElement === list);
    expect(portal).toHaveTextContent('portal');
    expect(within(portal!).getByRole('list', { name: 'Pontos de atenção de portal' })).toHaveTextContent(
      'Nenhuma ferramenta vinculada'
    );

    expect(loja).toHaveTextContent('loja');
    expect(within(loja!).getByText('Sem pontos de atenção')).toBeInTheDocument();
    expect(loja).toHaveTextContent('Issues: 1 aberto · 1 em andamento · 6 entregues em 8 semanas');
    expect(loja).not.toHaveTextContent('prazo vencido');
    expect(engine.calls.map((call) => call.method)).toEqual(['management.portfolio']);

    await expectNoA11yViolations(container);
  });
});
