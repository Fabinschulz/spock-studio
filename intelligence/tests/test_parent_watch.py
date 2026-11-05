"""O Python não fica órfão: termina quando o motor que o iniciou deixa de existir."""

import json
import os
import socket
import subprocess
import sys
import threading
import time
import unittest
import urllib.request

import _paths  # noqa: F401
from infrastructure.parent_watch import watch_parent

SERVER = os.path.join(str(_paths.ROOT), "server.py")
SLEEPER = [sys.executable, "-c", "import time; time.sleep(120)"]


def _free_port() -> int:
    with socket.socket() as probe:
        probe.bind(("127.0.0.1", 0))
        return probe.getsockname()[1]


class ParentWatchTests(unittest.TestCase):
    def test_avisa_quando_o_processo_observado_termina_e_so_entao(self) -> None:
        parent = subprocess.Popen(SLEEPER)
        gone = threading.Event()
        try:
            watch_parent(parent.pid, gone.set, interval=0.05)
            self.assertFalse(gone.wait(0.4), "o processo ainda vive")
            parent.kill()
            parent.wait()
            self.assertTrue(gone.wait(5), "o fim do processo não foi percebido")
        finally:
            if parent.poll() is None:
                parent.kill()
                parent.wait()

    def test_chama_o_aviso_uma_vez(self) -> None:
        parent = subprocess.Popen(SLEEPER)
        calls: list[int] = []
        try:
            thread = watch_parent(parent.pid, lambda: calls.append(1), interval=0.05)
            parent.kill()
            parent.wait()
            thread.join(5)
            time.sleep(0.2)
            self.assertEqual(calls, [1])
            self.assertFalse(thread.is_alive())
        finally:
            if parent.poll() is None:
                parent.kill()
                parent.wait()

    def test_servidor_real_termina_quando_o_motor_some(self) -> None:
        parent = subprocess.Popen(SLEEPER)
        token = "t" * 32
        port = _free_port()
        env = {**os.environ, "MARILIZ_AI_TOKEN": token}
        server = subprocess.Popen(
            [sys.executable, SERVER, "--port", str(port), "--parent-pid", str(parent.pid)],
            env=env,
            stdin=subprocess.DEVNULL,
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
        )
        try:
            request = urllib.request.Request(f"http://127.0.0.1:{port}/health", headers={"Authorization": f"Bearer {token}"})
            deadline = time.time() + 30
            while True:
                self.assertIsNone(server.poll(), "o servidor caiu antes de responder")
                try:
                    with urllib.request.urlopen(request, timeout=2) as response:
                        self.assertEqual(json.load(response)["status"], "ok")
                    break
                except OSError:
                    self.assertLess(time.time(), deadline, "o servidor não respondeu")
                    time.sleep(0.1)

            # Enquanto o motor vive, o servidor fica.
            time.sleep(2.5)
            self.assertIsNone(server.poll())

            parent.kill()
            parent.wait()
            self.assertEqual(server.wait(timeout=15), 0)
        finally:
            for process in (server, parent):
                if process.poll() is None:
                    process.kill()
                    process.wait()

    def test_sem_o_argumento_nada_e_vigiado(self) -> None:
        token = "t" * 32
        port = _free_port()
        server = subprocess.Popen(
            [sys.executable, SERVER, "--port", str(port)],
            env={**os.environ, "MARILIZ_AI_TOKEN": token},
            stdin=subprocess.DEVNULL,
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
        )
        try:
            time.sleep(3)
            self.assertIsNone(server.poll())
        finally:
            server.kill()
            server.wait()


if __name__ == "__main__":
    unittest.main()
