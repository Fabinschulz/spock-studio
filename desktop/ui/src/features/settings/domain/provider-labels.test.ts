import multiFixture from '@contracts/fixtures/models/connection-multi.json';
import { describe, expect, it } from 'vitest';

import { modelConnectionSchema } from '@/shared/schema';

import {
  defaultModelOf,
  describeProviderState,
  protectionText,
  purposeModelOptions,
  keySource,
  readKey
} from './provider-labels';

const connection = modelConnectionSchema.parse(multiFixture);

describe('describeProviderState', () => {
  it('cada estado tem texto e tom, e conferir com erro é perigo, não sucesso', () => {
    expect(describeProviderState('connected')).toEqual({ label: 'Conectado', tone: 'success' });
    expect(describeProviderState('error')).toEqual({ label: 'Com erro', tone: 'danger' });
    expect(describeProviderState('untested').tone).toBe('neutral');
    expect(describeProviderState('none').label).toBe('Sem chave');
  });

  it('um estado que a interface não conhece aparece como veio, sem fingir que funciona', () => {
    expect(describeProviderState('novo')).toEqual({ label: 'novo', tone: 'neutral' });
  });
});

describe('protectionText', () => {
  it('diz onde a chave está e nada quando não há chave', () => {
    expect(protectionText('vault')).toBe('Guardada com a proteção do sistema operacional.');
    expect(protectionText('file')).toMatch(/sem a proteção do sistema/);
    expect(protectionText(null)).toBeNull();
  });
});

describe('purposeModelOptions', () => {
  it('traz só os modelos de provedores com chave, cada um com o nome do provedor', () => {
    const labels = purposeModelOptions(connection).map((option) => option.label);

    expect(labels).toContain('GPT-4o mini (OpenAI, padrão)');
    expect(labels).toContain('Claude Opus 5.5 (Anthropic)');
    expect(labels.some((label) => label.includes('Gemini'))).toBe(false);
  });

  it('marca como padrão só o modelo padrão do provedor padrão', () => {
    const marked = purposeModelOptions(connection).filter((option) => option.label.endsWith(', padrão)'));

    expect(marked.map((option) => option.value)).toEqual(['gpt-4o-mini']);
    expect(defaultModelOf(connection)).toBe('gpt-4o-mini');
  });

  it('sem nenhuma chave não há opção nem modelo padrão', () => {
    const none = {
      ...connection,
      providers: connection.providers.map((provider) => ({ ...provider, configured: false, isDefault: false }))
    };

    expect(purposeModelOptions(none)).toEqual([]);
    expect(defaultModelOf(none)).toBeNull();
  });
});

describe('readKey e keySource', () => {
  it('tira os espaços de quem cola e só aceita o formato de uma chave', () => {
    expect(readKey('  abcdefghij_klmnopqrst-123  ')).toMatchObject({ key: 'abcdefghij_klmnopqrst-123', ready: true });
    expect(readKey('')).toMatchObject({ ready: false, malformed: false });
    expect(readKey('curta demais')).toMatchObject({ ready: false, malformed: true });
    expect(readKey('curta demais').helper).toMatch(/^Confira se copiou a chave inteira\./);
  });

  it('diz onde conseguir a chave de cada provedor, e um texto geral para os outros', () => {
    expect(keySource('anthropic')).toMatch(/console\.anthropic\.com/);
    expect(keySource('outro')).toBe('Crie a chave na área de chaves de API do site do provedor.');
  });
});
