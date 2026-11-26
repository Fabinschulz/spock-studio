import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router';
import { describe, expect, it } from 'vitest';

import { AppProviders } from '@/shared/components';
import { expectNoA11yViolations } from '@/test/a11y';

import { AttentionStrip } from './attention-strip';

const renderStrip = (
  attention: Parameters<typeof AttentionStrip>[0]['attention'],
  oldestReadAt: string | null = null
) =>
  render(
    <AppProviders>
      <MemoryRouter>
        <AttentionStrip attention={attention} oldestReadAt={oldestReadAt} />
      </MemoryRouter>
    </AppProviders>
  );

describe('Faixa do que pede atenção', () => {
  it('sem nada a apontar e com dados, diz que nada pede atenção', async () => {
    const { container } = renderStrip({ hasData: true, items: [] }, '2026-11-20T12:00:00Z');

    expect(screen.getByText('Nada pede atenção agora')).toBeInTheDocument();
    expect(screen.getByText(/Leitura mais antiga das fontes:/)).toBeInTheDocument();
    await expectNoA11yViolations(container);
  });

  it('sem dado sincronizado, não diz que nada pede atenção: diz que falta dado e como ter', () => {
    renderStrip({ hasData: false, items: [] });

    expect(screen.queryByText('Nada pede atenção agora')).not.toBeInTheDocument();
    expect(screen.getByText(/Ainda não há dados sincronizados/)).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Conexões › Serviços' })).toHaveAttribute('href', '/integracoes');
  });

  it('lista cada ponto de atenção', () => {
    renderStrip({
      hasData: true,
      items: [
        { id: 'risks', text: '2 riscos ativos.' },
        { id: 'stale', text: 'Dados lidos há 9 dias.' }
      ]
    });

    const list = screen.getByRole('list', { name: 'Pontos de atenção do projeto' });
    expect(list.children).toHaveLength(2);
    expect(list).toHaveTextContent('Dados lidos há 9 dias.');
  });
});
