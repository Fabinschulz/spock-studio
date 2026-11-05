"""Encerra o servidor quando o processo que o iniciou (o motor .NET) deixa de existir.

O motor fecha a entrada padrão do Python logo depois de subi-lo, então o fim do pipe não serve de sinal. Sem este vigia, um
motor finalizado à força (o host espera um prazo e mata o processo) deixaria o Python vivo, com o interpretador e o SQLite do
checkpoint abertos: o instalador não consegue trocar esses arquivos numa atualização, e a porta e o token ficam pendurados.

Só biblioteca padrão. No Windows espera por um handle do processo, que não confunde um processo novo com o antigo mesmo que o
número seja reaproveitado; nos demais sistemas consulta o número com ``kill(pid, 0)``. Nunca usar ``os.kill(pid, 0)`` no
Windows: lá o sinal 0 termina o processo.
"""

from __future__ import annotations

import os
import threading
import time
from typing import Callable

_ACCESS_DENIED = 5
_WAIT_TIMEOUT = 0x00000102
_SYNCHRONIZE = 0x00100000


def _posix_probe(pid: int) -> Callable[[], bool]:
    def gone() -> bool:
        try:
            os.kill(pid, 0)
        except ProcessLookupError:
            return True
        except PermissionError:
            return False
        return False

    return gone


def _windows_probe(pid: int) -> Callable[[], bool]:
    import ctypes
    from ctypes import wintypes

    kernel = ctypes.WinDLL("kernel32", use_last_error=True)
    kernel.OpenProcess.argtypes = [wintypes.DWORD, wintypes.BOOL, wintypes.DWORD]
    kernel.OpenProcess.restype = wintypes.HANDLE
    kernel.WaitForSingleObject.argtypes = [wintypes.HANDLE, wintypes.DWORD]
    kernel.WaitForSingleObject.restype = wintypes.DWORD

    handle = kernel.OpenProcess(_SYNCHRONIZE, False, pid)
    if not handle:
        # Sem permissão para observar não é prova de que o motor morreu: o servidor continua.
        denied = ctypes.get_last_error() == _ACCESS_DENIED
        return lambda: not denied

    def gone() -> bool:
        return kernel.WaitForSingleObject(handle, 0) != _WAIT_TIMEOUT

    return gone


def watch_parent(pid: int, on_gone: Callable[[], None], interval: float = 2.0) -> threading.Thread:
    """Chama ``on_gone`` uma vez, quando o processo ``pid`` não existir mais. Roda numa thread que não segura o encerramento."""
    probe = _windows_probe(pid) if os.name == "nt" else _posix_probe(pid)

    def loop() -> None:
        while True:
            if probe():
                on_gone()
                return
            time.sleep(interval)

    thread = threading.Thread(target=loop, name="parent-watch", daemon=True)
    thread.start()
    return thread
