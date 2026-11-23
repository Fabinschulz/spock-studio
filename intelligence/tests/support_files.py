import sys
import tempfile
import time

# O fim da execução apaga o checkpoint (e o SQLite apaga o -wal e o -shm ao fechar). No Windows, um arquivo
# recém-apagado ainda segura a pasta por alguns milissegundos (antivírus, indexador), e a limpeza falha com
# "a pasta não está vazia". Medido: some em menos de 1 ms. Um handle de fato vazado segura bem mais que a
# última espera, e a limpeza continua falhando.
_WAITS = (0.01, 0.05, 0.1, 0.25, 0.5) if sys.platform == "win32" else ()


class TemporaryDirectory(tempfile.TemporaryDirectory):
    def cleanup(self) -> None:
        for wait in _WAITS:
            try:
                super().cleanup()
                return
            except OSError:
                time.sleep(wait)
        super().cleanup()
