import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';

import { AppProviders } from '@/shared/components';
import { expectNoA11yViolations } from '@/test/a11y';

import { GLOSSARY } from './glossary';
import { Term } from './Term';

function renderTerm() {
  return render(
    <AppProviders>
      <p>
        O projeto recebe o <Term id="harness" /> antes de qualquer mudança.
      </p>
    </AppProviders>
  );
}

describe('Term', () => {
  it('explica o termo a quem chega pelo teclado, sem trocar o nome dele', async () => {
    renderTerm();

    await userEvent.tab();

    const term = screen.getByRole('button', { name: 'Harness' });
    expect(term).toHaveFocus();
    expect(await screen.findByRole('tooltip')).toHaveTextContent(GLOSSARY.harness.text);
    expect(term).toHaveAccessibleDescription(GLOSSARY.harness.text);
  });

  it('explica ao passar o mouse e aceita outro texto para o mesmo termo', async () => {
    render(
      <AppProviders>
        <Term id="token">8.000 tokens</Term>
      </AppProviders>
    );

    await userEvent.hover(screen.getByRole('button', { name: '8.000 tokens' }));

    expect(await screen.findByRole('tooltip')).toHaveTextContent(GLOSSARY.token.text);
  });

  it('todo termo do glossário tem nome e explicação', () => {
    for (const entry of Object.values(GLOSSARY)) {
      expect(entry.label.length).toBeGreaterThan(0);
      expect(entry.text.length).toBeGreaterThan(20);
    }
  });

  it('não tem violações estruturais de acessibilidade', async () => {
    const { container } = renderTerm();

    await expectNoA11yViolations(container);
  });
});
