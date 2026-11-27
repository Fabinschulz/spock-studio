import { render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';

import bootProject from '@contracts/fixtures/boot/project.json';
import { bootResponseSchema } from '@/shared/schema';

import { BootResultCard } from './boot-result-card';

afterEach(() => vi.restoreAllMocks());

describe('BootResultCard', () => {
  it('linhas repetidas vindas do motor não geram chave duplicada', () => {
    const error = vi.spyOn(console, 'error').mockImplementation(() => undefined);
    const record = bootResponseSchema.parse(bootProject).boot;
    record.result!.problems = ['Mesma linha.', 'Mesma linha.'];
    record.result!.consulted = ['Igual.', 'Igual.'];

    render(<BootResultCard record={record} onOpen={vi.fn()} onChoose={vi.fn()} onDismiss={vi.fn()} />);

    expect(screen.getAllByText('Mesma linha.')).toHaveLength(2);
    expect(screen.getAllByText('Igual.')).toHaveLength(2);
    expect(error.mock.calls.flat().join(' ')).not.toMatch(/same key|unique "key"/);
  });

  it('um Boot sem resultado (interrompido) avisa em vez de mostrar um cartão vazio', () => {
    const record = bootResponseSchema.parse(bootProject).boot;
    render(
      <BootResultCard record={{ ...record, result: null }} onOpen={vi.fn()} onChoose={vi.fn()} onDismiss={vi.fn()} />
    );

    expect(screen.getByText(/interrompido antes de terminar/)).toBeInTheDocument();
  });
});

describe('BootResultCard · etapas', () => {
  it('sem etapas, como no cartão de escolha, não oferece "Ver as etapas (0)"', () => {
    const record = bootResponseSchema.parse(bootProject).boot;
    render(
      <BootResultCard record={{ ...record, steps: [] }} onOpen={vi.fn()} onChoose={vi.fn()} onDismiss={vi.fn()} />
    );

    expect(screen.queryByText(/Ver as etapas/)).not.toBeInTheDocument();
  });
});
