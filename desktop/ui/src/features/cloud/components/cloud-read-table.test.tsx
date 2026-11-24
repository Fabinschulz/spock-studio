import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { cloudReadResultSchema } from '@/shared/schema';

import { CloudReadTable } from './cloud-read-table';

describe('CloudReadTable', () => {
  it('escreve sim e não nas colunas flag e mostra o texto das outras como veio', () => {
    const { result } = cloudReadResultSchema.parse({
      result: {
        connectionId: 'cloud-1',
        connectionName: 'AWS dev',
        provider: 'aws',
        account: '123456789012',
        capability: 'aws.ec2.vpcs',
        region: 'sa-east-1',
        columns: [
          { id: 'id', label: 'Id', kind: 'text' },
          { id: 'default', label: 'Padrão', kind: 'flag' },
          { id: 'state', label: 'Estado' }
        ],
        rows: [
          ['vpc-1', 'true', 'available'],
          ['vpc-2', 'false', null]
        ],
        truncated: false,
        readAt: '2026-11-24T12:00:00+00:00'
      }
    });

    render(<CloudReadTable read={result} provider={undefined} />);

    const cells = screen.getAllByRole('cell').map((cell) => cell.textContent);
    expect(cells).toEqual(['vpc-1', 'sim', 'available', 'vpc-2', 'não', '-']);
  });
});
