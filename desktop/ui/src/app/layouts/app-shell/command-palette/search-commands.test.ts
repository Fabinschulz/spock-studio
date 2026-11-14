import { describe, expect, it } from 'vitest';

import type { Command } from './commands';
import { searchCommands } from './search-commands';

const command = (id: string, label: string, hint = 'Engenharia', keywords = ''): Command => ({
  id,
  label,
  hint,
  keywords,
  action: { kind: 'go', to: `/${id}` }
});

const commands = [
  command('catalogo', 'Catálogo'),
  command('configurar', 'Configurar'),
  command('config', 'Configurações', 'Plataforma'),
  command('loja', 'loja-api', 'Abrir projeto', '~\\dev\\loja-api')
];

const ids = (result: readonly Command[]) => result.map((entry) => entry.id);

describe('searchCommands', () => {
  it('sem texto devolve tudo, na ordem de origem', () => {
    expect(ids(searchCommands(commands, '  '))).toEqual(['catalogo', 'configurar', 'config', 'loja']);
  });

  it('ignora acento e caixa', () => {
    expect(ids(searchCommands(commands, 'CATALOGO'))).toEqual(['catalogo']);
    expect(ids(searchCommands(commands, 'configuraç'))).toEqual(['config']);
  });

  it('exige todas as palavras, em qualquer ordem', () => {
    expect(ids(searchCommands(commands, 'plataforma config'))).toEqual(['config']);
    expect(ids(searchCommands(commands, 'config inexistente'))).toEqual([]);
  });

  it('o título que começa com o texto vem antes do que só o contém', () => {
    const list = [command('a', 'Visão geral do portfólio'), command('b', 'Portfólio')];

    expect(ids(searchCommands(list, 'portfolio'))).toEqual(['b', 'a']);
  });

  it('empates mantêm a ordem de origem', () => {
    expect(ids(searchCommands(commands, 'conf'))).toEqual(['configurar', 'config']);
  });

  it('encontra um projeto pelo caminho', () => {
    expect(ids(searchCommands(commands, 'dev loja'))).toEqual(['loja']);
  });
});
