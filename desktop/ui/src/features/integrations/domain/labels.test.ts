import { describe, expect, it } from 'vitest';

import { githubConnection, syncedIntegration } from '@/test/fake-integration-engine';

import { impactText, itemsSummary, kindCounts, syncCounts } from './labels';

const sync = syncedIntegration.lastSync!;
const github = {
  provider: githubConnection.provider,
  name: 'GitHub',
  version: '1.0.0',
  settings: [],
  secret: { name: 'token', label: 'Token', hint: '' },
  resource: { name: 'repository', label: 'Repositório', hint: '' },
  dataKinds: [
    { id: 'issues', label: 'Issues' },
    { id: 'pullRequests', label: 'Pull requests' }
  ],
  scopesNote: '',
  impact: 'read'
};

describe('rótulos da central de integrações', () => {
  it('diz o impacto sem rodeio e não chama de leitura o que a tela não conhece', () => {
    expect(impactText('read', 'GitHub')).toBe(
      'Leitura: o Spock Studio só lê do GitHub. Não cria, altera nem apaga nada lá.'
    );
    expect(impactText('write', 'GitHub')).toBe('Altera dados: o Spock Studio pode alterar dados no GitHub.');
    expect(impactText('unknown', 'GitHub')).toContain('não afirma o que ela pode fazer');
    expect(impactText('algo-novo', 'GitHub')).not.toContain('Leitura');
  });

  it('conta só as parcelas que existem numa sincronização', () => {
    expect(syncCounts({ ...sync, created: 1, updated: 2, unchanged: 5 })).toBe('1 novo, 2 atualizados, 5 sem mudança');
    expect(syncCounts({ ...sync, created: 0, updated: 1, unchanged: 0 })).toBe('1 atualizado');
    expect(syncCounts({ ...sync, created: 0, updated: 0, unchanged: 0 })).toBe('nada novo');
  });

  it('usa o rótulo do conector para cada tipo e o próprio id quando o conector não o conhece', () => {
    expect(kindCounts(syncedIntegration, github)).toEqual(['Issues: 1', 'Pull requests: 1']);
    expect(kindCounts(syncedIntegration, undefined)).toEqual(['issues: 1', 'pullRequests: 1']);
  });

  it('distingue o vínculo nunca sincronizado do recorte que veio vazio', () => {
    const empty = { total: 0, byKind: {}, byStatusCategory: {}, lastSyncedAt: null };
    expect(itemsSummary({ ...syncedIntegration, items: empty, lastSync: null }, github)).toBe('Ainda não sincronizada');
    expect(itemsSummary({ ...syncedIntegration, items: empty }, github)).toBe('Nenhum item no recorte');
  });
});
