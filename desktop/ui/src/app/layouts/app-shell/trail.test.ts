import { describe, expect, it } from 'vitest';

import { trailOf } from './trail';

describe('trailOf', () => {
  it('o início não tem trilha', () => {
    expect(trailOf('/')).toEqual([]);
  });

  it('uma tela do menu mostra o grupo, que leva à tela inicial dele, e o nome da tela', () => {
    expect(trailOf('/projeto/harness')).toEqual([{ label: 'Engenharia', to: '/projeto' }, { label: 'Harness' }]);
    expect(trailOf('/gestao/relatorio')).toEqual([{ label: 'Gestão', to: '/gestao' }, { label: 'Relatório' }]);
  });

  it('a Plataforma não tem tela própria: o nome dela é só texto', () => {
    expect(trailOf('/integracoes')).toEqual([{ label: 'Plataforma' }, { label: 'Integrações' }]);
  });

  it('a execução passa pela lista de execuções', () => {
    expect(trailOf('/execucao/run-1')).toEqual([
      { label: 'Engenharia', to: '/projeto' },
      { label: 'Execuções', to: '/execucoes' },
      { label: 'Execução' }
    ]);
  });

  it('uma rota que não está no menu fica sem trilha', () => {
    expect(trailOf('/nao-existe')).toEqual([]);
  });
});
