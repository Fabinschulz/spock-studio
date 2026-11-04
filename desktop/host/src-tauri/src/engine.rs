//! Ciclo de vida do runtime .NET (sidecar) e correlação de requisições e respostas. Onde estão os recursos
//! empacotados fica em `resources`; como o estado chega à UI, em `status`.

use std::{
    collections::HashMap,
    sync::{
        atomic::{AtomicBool, AtomicU64, Ordering},
        Arc, Mutex,
    },
    time::Duration,
};

use serde_json::{json, Value};
use tauri::{AppHandle, Emitter, Manager};
use tauri_plugin_shell::{
    process::{CommandChild, CommandEvent},
    ShellExt,
};
use tokio::sync::oneshot;

use crate::lock;
use crate::protocol::{parse_engine_line, EngineMessage, HostError, PROTOCOL_VERSION};
use crate::resources::packaged_runtime;
use crate::status::Status;

// Descoberta e plano leem o projeto e consultam ferramentas; execuções longas respondem na hora e avisam por eventos.
const REQUEST_TIMEOUT: Duration = Duration::from_secs(120);
const SIDECAR: &str = "mariliz-runtime";
const SHUTDOWN_GRACE: Duration = Duration::from_secs(10);

#[derive(Default)]
struct Inner {
    child: Mutex<Option<CommandChild>>,
    pending: Mutex<HashMap<String, oneshot::Sender<Value>>>,
    status: Mutex<Option<Status>>,
    next_id: AtomicU64,
    restarts: AtomicU64,
    shutting_down: AtomicBool,
    terminated: AtomicBool,
}

#[derive(Clone, Default)]
pub struct Engine(Arc<Inner>);

impl Engine {
    pub fn status(&self) -> Status {
        lock(&self.0.status).unwrap_or(Status::Starting)
    }

    fn set_status(&self, app: &AppHandle, status: Status) {
        *lock(&self.0.status) = Some(status);
        let _ = app.emit("engine_status", status.as_str());
    }

    /// Inicia o motor e faz o handshake. Falhas viram o estado `crashed`; a UI mostra o diagnóstico.
    pub fn boot(&self, app: AppHandle) {
        let engine = self.clone();
        tauri::async_runtime::spawn(async move {
            if let Err(error) = engine.start_and_handshake(&app).await {
                eprintln!(
                    "[host] motor indisponível: {} ({})",
                    error.message, error.code
                );
                engine.set_status(&app, Status::Crashed);
            }
        });
    }

    async fn start_and_handshake(&self, app: &AppHandle) -> Result<(), HostError> {
        self.set_status(app, Status::Starting);
        self.spawn_process(app)?;

        let hello = self
            .request(
                "engine.hello",
                json!({ "protocol": [PROTOCOL_VERSION], "hostVersion": env!("CARGO_PKG_VERSION") }),
            )
            .await?;

        if hello.get("protocol").and_then(Value::as_u64) != Some(PROTOCOL_VERSION) {
            return Err(HostError::new(
                "UNSUPPORTED_PROTOCOL",
                "O motor não fala a versão de protocolo esperada.",
            ));
        }
        eprintln!(
            "[host] motor pronto (protocolo {}, versão {})",
            PROTOCOL_VERSION,
            hello
                .get("engineVersion")
                .and_then(Value::as_str)
                .unwrap_or("?")
        );
        self.set_status(app, Status::Ready);
        Ok(())
    }

    fn spawn_process(&self, app: &AppHandle) -> Result<(), HostError> {
        let mut command = app.shell().sidecar(SIDECAR).map_err(|_| {
            HostError::new(
                "ENGINE_NOT_FOUND",
                "O executável do motor não foi encontrado.",
            )
        })?;
        if let Ok(dir) = app.path().resource_dir() {
            let (script, python) = packaged_runtime(&dir);
            if let Some(script) = script {
                command = command.env("MARILIZ_AI", script);
            }
            if let Some(python) = python {
                command = command.env("MARILIZ_PYTHON", python);
            }
        }
        let (mut rx, child) = command.spawn().map_err(|_| {
            HostError::new("ENGINE_START_FAILED", "Não foi possível iniciar o motor.")
        })?;

        *lock(&self.0.child) = Some(child);

        let engine = self.clone();
        let app = app.clone();
        tauri::async_runtime::spawn(async move {
            while let Some(event) = rx.recv().await {
                match event {
                    CommandEvent::Stdout(line) => engine.on_stdout(&app, &line),
                    CommandEvent::Stderr(line) => {
                        eprintln!("[motor] {}", String::from_utf8_lossy(&line).trim_end());
                    }
                    CommandEvent::Error(error) => {
                        eprintln!("[host] erro no processo do motor: {error}")
                    }
                    CommandEvent::Terminated(payload) => {
                        engine.on_terminated(&app, payload.code);
                        break;
                    }
                    _ => {}
                }
            }
        });
        Ok(())
    }

    fn on_stdout(&self, app: &AppHandle, line: &[u8]) {
        match parse_engine_line(line) {
            Some(EngineMessage::Response { id, value }) => {
                if let Some(tx) = lock(&self.0.pending).remove(&id) {
                    let _ = tx.send(value);
                }
            }
            Some(EngineMessage::Event(value)) => {
                let _ = app.emit("engine_event", value);
            }
            None => eprintln!("[host] linha descartada do stdout do motor"),
        }
    }

    fn on_terminated(&self, app: &AppHandle, code: Option<i32>) {
        self.0.terminated.store(true, Ordering::SeqCst);
        *lock(&self.0.child) = None;
        // Descartar os remetentes faz as requisições pendentes falharem com ENGINE_STOPPED.
        lock(&self.0.pending).clear();

        if self.0.shutting_down.load(Ordering::SeqCst) {
            return;
        }
        eprintln!("[host] o motor encerrou (código {code:?})");
        self.set_status(app, Status::Crashed);

        // Uma tentativa automática; depois disso a UI mostra o diagnóstico.
        if self.0.restarts.fetch_add(1, Ordering::SeqCst) == 0 {
            self.boot(app.clone());
        }
    }

    pub async fn request(&self, method: &str, params: Value) -> Result<Value, HostError> {
        let id = format!("h-{}", self.0.next_id.fetch_add(1, Ordering::SeqCst) + 1);
        let (tx, rx) = oneshot::channel();
        lock(&self.0.pending).insert(id.clone(), tx);

        let mut line = serde_json::to_vec(&json!({
            "v": PROTOCOL_VERSION, "kind": "request", "id": id, "method": method, "params": params
        }))
        .map_err(|_| {
            HostError::new(
                "INVALID_MESSAGE",
                "Não foi possível serializar a requisição.",
            )
        })?;
        line.push(b'\n');

        {
            let mut guard = lock(&self.0.child);
            let Some(child) = guard.as_mut() else {
                lock(&self.0.pending).remove(&id);
                return Err(HostError::new(
                    "ENGINE_NOT_RUNNING",
                    "O motor não está em execução.",
                ));
            };
            if child.write(&line).is_err() {
                lock(&self.0.pending).remove(&id);
                return Err(HostError::new(
                    "ENGINE_WRITE_FAILED",
                    "Não foi possível falar com o motor.",
                ));
            }
        }

        let response = match tokio::time::timeout(REQUEST_TIMEOUT, rx).await {
            Ok(Ok(value)) => value,
            Ok(Err(_)) => {
                return Err(HostError::new(
                    "ENGINE_STOPPED",
                    "O motor encerrou antes de responder.",
                ))
            }
            Err(_) => {
                lock(&self.0.pending).remove(&id);
                return Err(HostError::new(
                    "ENGINE_TIMEOUT",
                    "O motor demorou demais para responder.",
                ));
            }
        };

        if let Some(error) = response.get("error") {
            let code = error
                .get("code")
                .and_then(Value::as_str)
                .unwrap_or("UNKNOWN");
            let message = error
                .get("message")
                .and_then(Value::as_str)
                .unwrap_or("Falha no motor.");
            return Err(HostError::new(code, message));
        }
        response
            .get("result")
            .cloned()
            .ok_or_else(|| HostError::new("INVALID_MESSAGE", "Resposta do motor sem resultado."))
    }

    /// Pede o encerramento ordenado e espera o motor terminar. Uma aplicação em andamento é cancelada e
    /// desfeita pelo motor antes de ele sair; só depois do prazo o processo é finalizado à força.
    pub fn shutdown(&self) {
        self.0.shutting_down.store(true, Ordering::SeqCst);
        self.0.terminated.store(false, Ordering::SeqCst);
        {
            let mut guard = lock(&self.0.child);
            let Some(child) = guard.as_mut() else { return };
            let _ = child.write(b"{\"v\":1,\"kind\":\"request\",\"id\":\"h-shutdown\",\"method\":\"engine.shutdown\"}\n");
        }

        let deadline = std::time::Instant::now() + SHUTDOWN_GRACE;
        while std::time::Instant::now() < deadline && !self.0.terminated.load(Ordering::SeqCst) {
            std::thread::sleep(Duration::from_millis(50));
        }

        if let Some(child) = lock(&self.0.child).take() {
            let _ = child.kill();
        }
    }
}
