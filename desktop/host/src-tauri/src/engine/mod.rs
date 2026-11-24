//! O motor .NET como processo filho: `process` sobe e encerra, `rpc` correlaciona pedidos e respostas, `lifecycle`
//! decide o que cada transição faz e `timeouts` diz quanto o host espera por método. Aqui só se liga uma coisa à outra.

mod lifecycle;
mod process;
mod rpc;
mod timeouts;

use std::sync::atomic::AtomicU64;
use std::sync::{Arc, Mutex};
use std::time::{Duration, Instant};

use serde_json::{json, Value};
use tauri::{AppHandle, Emitter};
use tauri_plugin_shell::process::CommandEvent;
use tokio::sync::watch;

use crate::diagnostic_log::Diagnostics;
use crate::lock;
use crate::protocol::{
    encode_request, error_code, method, parse_engine_line, EngineMessage, HostError,
    PROTOCOL_VERSION,
};
use crate::status::Status;
use crate::writer::Writer;
use lifecycle::Lifecycle;
use process::Process;
use rpc::Rpc;

// O handshake leva poucos segundos; autorizar um projeto espera por ele em vez de falhar enquanto o motor sobe.
pub const STARTUP_WAIT: Duration = Duration::from_secs(20);
const SHUTDOWN_GRACE: Duration = Duration::from_secs(10);
const SHUTDOWN_POLL: Duration = Duration::from_millis(50);

struct Inner {
    /// Travado só para trocar ou copiar o processo, nunca durante uma escrita.
    process: Mutex<Option<Process>>,
    rpc: Rpc,
    /// O que o motor anunciou no último hello; vazio até lá, e todo método fica com o padrão.
    deadlines: Mutex<timeouts::Deadlines>,
    lifecycle: watch::Sender<Lifecycle>,
    restarts: AtomicU64,
    diagnostics: Diagnostics,
}

#[derive(Clone)]
pub struct Engine(Arc<Inner>);

impl Engine {
    pub fn new(diagnostics: Diagnostics) -> Self {
        Self(Arc::new(Inner {
            process: Mutex::default(),
            rpc: Rpc::default(),
            deadlines: Mutex::default(),
            lifecycle: watch::Sender::new(Lifecycle::Starting),
            restarts: AtomicU64::new(0),
            diagnostics,
        }))
    }

    pub fn status(&self) -> Status {
        self.0.lifecycle.borrow().status()
    }

    /// A UI pede o último projeto logo ao abrir; antes de o processo do motor existir, o pedido falharia na hora com
    /// `ENGINE_NOT_RUNNING` e o projeto não seria reaberto. Pronto ou caído, segue: o pedido responde por si.
    pub async fn wait_until_started(&self, limit: Duration) {
        let mut changes = self.0.lifecycle.subscribe();
        let _ = tokio::time::timeout(
            limit,
            changes.wait_for(|phase| *phase != Lifecycle::Starting),
        )
        .await;
    }

    fn set(&self, app: &AppHandle, next: Lifecycle) {
        self.0.lifecycle.send_replace(next);
        let _ = app.emit("engine_status", next.status().as_str());
    }

    /// Um encerramento do aplicativo durante a partida vence: o resultado do handshake chega tarde e não o desfaz.
    fn set_if_starting(&self, app: &AppHandle, next: Lifecycle) -> bool {
        let changed = self.0.lifecycle.send_if_modified(|phase| {
            if *phase != Lifecycle::Starting {
                return false;
            }
            *phase = next;
            true
        });
        if changed {
            let _ = app.emit("engine_status", next.status().as_str());
        }
        changed
    }

    pub fn boot(&self, app: AppHandle) {
        let engine = self.clone();
        tauri::async_runtime::spawn(async move {
            // Uma reinicialização que chega depois de o aplicativo começar a fechar não sobe outro motor.
            let starting = engine.0.lifecycle.send_if_modified(|phase| {
                if matches!(phase, Lifecycle::ShuttingDown | Lifecycle::Stopped) {
                    return false;
                }
                *phase = Lifecycle::Starting;
                true
            });
            if !starting {
                return;
            }
            let _ = app.emit("engine_status", Status::Starting.as_str());
            let Err(error) = engine.start_and_handshake(&app).await else {
                return;
            };
            engine.0.diagnostics.host(&format!(
                "motor indisponível: {} ({})",
                error.message, error.code
            ));
            let (next, restart) = lifecycle::on_handshake_error(&error.code, &engine.0.restarts);
            if !engine.set_if_starting(&app, next) {
                return;
            }
            // Sem isto, um motor que não completou o handshake ficaria vivo, sem dono e com a UI em `crashed`.
            engine.kill_process();
            if restart {
                engine.boot(app);
            }
        });
    }

    async fn start_and_handshake(&self, app: &AppHandle) -> Result<(), HostError> {
        let (process, events) = process::spawn(app)?;
        let pid = process.pid;
        *lock(&self.0.process) = Some(process);
        self.pump(app, pid, events);

        let hello = self
            .0
            .rpc
            .call(
                self.writer(),
                method::ENGINE_HELLO,
                Some(json!({ "protocol": [PROTOCOL_VERSION], "hostVersion": env!("CARGO_PKG_VERSION") })),
                timeouts::HANDSHAKE_TIMEOUT,
            )
            .await?;

        if hello.get("protocol").and_then(Value::as_u64) != Some(PROTOCOL_VERSION) {
            return Err(HostError::new(
                error_code::UNSUPPORTED_PROTOCOL,
                "O motor não fala a versão de protocolo esperada.",
            ));
        }
        *lock(&self.0.deadlines) = timeouts::announced(&hello);
        let version = hello
            .get("engineVersion")
            .and_then(Value::as_str)
            .unwrap_or("?");
        if self.set_if_starting(
            app,
            Lifecycle::Ready {
                since: Instant::now(),
            },
        ) {
            self.0.diagnostics.host(&format!(
                "motor pronto (protocolo {PROTOCOL_VERSION}, versão {version})"
            ));
        }
        Ok(())
    }

    fn pump(
        &self,
        app: &AppHandle,
        pid: u32,
        mut events: tauri::async_runtime::Receiver<CommandEvent>,
    ) {
        let engine = self.clone();
        let app = app.clone();
        tauri::async_runtime::spawn(async move {
            while let Some(event) = events.recv().await {
                match event {
                    CommandEvent::Stdout(line) => engine.on_stdout(&app, &line),
                    CommandEvent::Stderr(line) => engine
                        .0
                        .diagnostics
                        .motor(String::from_utf8_lossy(&line).trim_end()),
                    CommandEvent::Error(error) => engine
                        .0
                        .diagnostics
                        .host(&format!("erro no processo do motor: {error}")),
                    CommandEvent::Terminated(payload) => {
                        engine.on_terminated(&app, pid, payload.code);
                        break;
                    }
                    _ => {}
                }
            }
        });
    }

    fn on_stdout(&self, app: &AppHandle, line: &[u8]) {
        match parse_engine_line(line) {
            Some(EngineMessage::Response { id, value }) => self.0.rpc.resolve(&id, value),
            Some(EngineMessage::Event(value)) => {
                let _ = app.emit("engine_event", value);
            }
            None => self
                .0
                .diagnostics
                .host("linha descartada do stdout do motor"),
        }
    }

    fn on_terminated(&self, app: &AppHandle, pid: u32, code: Option<i32>) {
        // Um processo antigo que termina depois de outro já ter subido não leva o novo junto.
        {
            let mut process = lock(&self.0.process);
            if process.as_ref().is_some_and(|current| current.pid == pid) {
                process.take();
            }
        }
        self.0.rpc.fail_all();

        let current = *self.0.lifecycle.borrow();
        let Some((next, restart)) = lifecycle::on_exit(current, &self.0.restarts) else {
            return;
        };
        if next == Lifecycle::Stopped {
            self.0.lifecycle.send_replace(next);
            return;
        }
        self.0
            .diagnostics
            .host(&format!("o motor encerrou (código {code:?})"));
        self.set(app, next);
        if restart {
            self.boot(app.clone());
        }
    }

    fn writer(&self) -> Option<Writer> {
        lock(&self.0.process)
            .as_ref()
            .map(|process| process.writer.clone())
    }

    fn kill_process(&self) -> Option<std::thread::JoinHandle<()>> {
        lock(&self.0.process).take().map(Process::kill)
    }

    pub async fn request(&self, method: &str, params: Value) -> Result<Value, HostError> {
        // Calculado antes do await: o lock não pode atravessar a espera pela resposta.
        let timeout = timeouts::request_timeout(method, &lock(&self.0.deadlines));
        self.0
            .rpc
            .call(self.writer(), method, Some(params), timeout)
            .await
    }

    /// Uma aplicação em andamento é cancelada e desfeita pelo motor antes de ele sair; só depois do prazo o processo é
    /// finalizado à força.
    pub fn shutdown(&self) {
        self.0.lifecycle.send_replace(Lifecycle::ShuttingDown);
        let Some(writer) = self.writer() else {
            return;
        };
        writer.send(encode_request("h-shutdown", method::ENGINE_SHUTDOWN, None));

        let deadline = Instant::now() + SHUTDOWN_GRACE;
        while Instant::now() < deadline && *self.0.lifecycle.borrow() != Lifecycle::Stopped {
            std::thread::sleep(SHUTDOWN_POLL);
        }
        if let Some(killing) = self.kill_process() {
            let _ = killing.join();
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn autorizar_espera_o_motor_subir_e_nao_espera_alem_do_prazo() {
        let engine = Engine::new(Diagnostics::default());
        let started = Instant::now();
        tauri::async_runtime::block_on(engine.wait_until_started(Duration::from_millis(300)));
        assert!(started.elapsed() >= Duration::from_millis(300));
        assert!(started.elapsed() < Duration::from_secs(3));

        let ready = engine.clone();
        std::thread::spawn(move || {
            std::thread::sleep(Duration::from_millis(150));
            ready.0.lifecycle.send_replace(Lifecycle::Ready {
                since: Instant::now(),
            });
        });
        let started = Instant::now();
        tauri::async_runtime::block_on(engine.wait_until_started(Duration::from_secs(10)));
        assert!(started.elapsed() < Duration::from_secs(5));
        assert_eq!(engine.status(), Status::Ready);

        engine.0.lifecycle.send_replace(Lifecycle::HandshakeFailed);
        let started = Instant::now();
        tauri::async_runtime::block_on(engine.wait_until_started(Duration::from_secs(10)));
        assert!(started.elapsed() < Duration::from_secs(1));
        assert_eq!(engine.status(), Status::Crashed);
    }

    #[test]
    fn sem_processo_o_pedido_falha_na_hora_e_o_encerramento_nao_espera() {
        let engine = Engine::new(Diagnostics::default());

        let error =
            tauri::async_runtime::block_on(engine.request("run.snapshot", json!({}))).unwrap_err();
        let started = Instant::now();
        engine.shutdown();

        assert_eq!(error.code, error_code::ENGINE_NOT_RUNNING);
        assert!(started.elapsed() < Duration::from_secs(1));
        assert_eq!(*engine.0.lifecycle.borrow(), Lifecycle::ShuttingDown);
    }
}
