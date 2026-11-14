import { beforeEach, describe, expect, it, vi } from 'vitest';

import { EngineError } from '@/shared/api';

import { tauriTerminalApi, type TerminalEvent } from './terminal-api';

const invoke = vi.fn();

class FakeChannel {
  onmessage: (message: unknown) => void = () => undefined;
}

vi.mock('@tauri-apps/api/core', () => ({
  invoke: (...args: unknown[]) => invoke(...args),
  Channel: FakeChannel
}));

beforeEach(() => {
  invoke.mockReset();
});

describe('terminal no host', () => {
  it('abre pelo id do recente, nunca por caminho, e repassa só eventos no formato combinado', async () => {
    invoke.mockResolvedValue({ sessionId: 't-1', shell: 'PowerShell' });
    const events: TerminalEvent[] = [];

    const opened = await tauriTerminalApi.open('r-abc', { cols: 80, rows: 24 }, (event) => events.push(event));

    expect(opened).toEqual({ sessionId: 't-1', shell: 'PowerShell' });
    const [command, args] = invoke.mock.calls[0]!;
    expect(command).toBe('terminal_open');
    expect(args).toMatchObject({ recentId: 'r-abc', cols: 80, rows: 24 });
    expect(Object.keys(args)).not.toContain('path');

    const channel = args.onEvent as FakeChannel;
    channel.onmessage({ kind: 'output', data: 'ok\r\n' });
    channel.onmessage({ kind: 'exit', code: 0 });
    channel.onmessage({ kind: 'desconhecido' });
    expect(events).toEqual([
      { kind: 'output', data: 'ok\r\n' },
      { kind: 'exit', code: 0 }
    ]);
  });

  it('o erro do host chega com o código e a mensagem dele', async () => {
    invoke.mockRejectedValue({ code: 'TERMINAL_LIMIT', message: 'Já há 4 terminais abertos.' });

    const error = await tauriTerminalApi.write('t-1', 'ls').catch((cause: unknown) => cause);

    expect(error).toBeInstanceOf(EngineError);
    expect(error).toMatchObject({ code: 'TERMINAL_LIMIT', message: 'Já há 4 terminais abertos.' });
  });

  it('escrever, redimensionar e fechar levam o id da sessão', async () => {
    invoke.mockResolvedValue(null);

    await tauriTerminalApi.write('t-1', 'dir\r');
    await tauriTerminalApi.resize('t-1', { cols: 100, rows: 30 });
    await tauriTerminalApi.close('t-1');

    expect(invoke.mock.calls).toEqual([
      ['terminal_write', { sessionId: 't-1', data: 'dir\r' }],
      ['terminal_resize', { sessionId: 't-1', cols: 100, rows: 30 }],
      ['terminal_close', { sessionId: 't-1' }]
    ]);
  });
});
