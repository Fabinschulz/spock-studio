use std::{
    collections::HashMap,
    sync::{
        atomic::{AtomicBool, AtomicU64, Ordering},
        Arc, Mutex,
    },
    time::{Duration, Instant},
};

use serde_json::{json, Value};
use tauri::{AppHandle, Emitter, Manager};
use tauri_plugin_shell::{
    process::{CommandChild, CommandEvent},
    ShellExt,
};
use tokio::sync::oneshot;

use crate::diagnostic_log::{record, Source};
use crate::lock;
use crate::protocol::{
    error_code, method, parse_engine_line, EngineMessage, HostError, PROTOCOL_VERSION,
};
use crate::resources::packaged_runtime;
use crate::status::Status;

// Descoberta e plano leem o projeto e consultam ferramentas; execuções longas respondem na hora e avisam por eventos.
const REQUEST_TIMEOUT: Duration = Duration::from_secs(120);
// A investigação de gestão conversa com o modelo por até 150 s no motor (o runtime de IA recebe 120 s). O host espera mais que o
// motor: se cortasse antes, a UI mostraria ENGINE_TIMEOUT enquanto o motor ainda termina e grava a resposta.
const INVESTIGATION_TIMEOUT: Duration = Duration::from_secs(180);
const INVESTIGATION_METHOD: &str = "management.investigate";
// O download do Terraform tem até 10 min no motor (HashiCorpTerraformToolchain); o host espera um pouco mais pelo mesmo motivo.
const TERRAFORM_INSTALL_TIMEOUT: Duration = Duration::from_secs(660);
const TERRAFORM_INSTALL_METHOD: &str = "iac.terraform.install";

fn request_timeout(method: &str) -> Duration {
    match method {
        INVESTIGATION_METHOD => INVESTIGATION_TIMEOUT,
        TERRAFORM_INSTALL_METHOD => TERRAFORM_INSTALL_TIMEOUT,
        _ => REQUEST_TIMEOUT,
    }
}
const SIDECAR: &str = "mariliz-runtime";
const SHUTDOWN_GRACE: Duration = Duration::from_secs(10);
// Um motor que ficou de pé por este tempo e caiu conta como uma queda nova, com direito a outra tentativa automática.
const STABLE_UPTIME: Duration = Duration::from_secs(60);
const MAX_QUICK_RESTARTS: u64 = 1;

#[derive(Default)]
struct Inner {
    child: Mutex<Option<CommandChild>>,
    pending: Mutex<HashMap<String, oneshot::Sender<Value>>>,
    status: Mutex<Option<Status>>,
    next_id: AtomicU64,
    restarts: AtomicU64,
    ready_since: Mutex<Option<Instant>>,
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
        if status == Status::Ready {
            *lock(&self.0.ready_since) = Some(Instant::now());
        }
        let _ = app.emit("engine_status", status.as_str());
    }

    pub fn boot(&self, app: AppHandle) {
        let engine = self.clone();
        tauri::async_runtime::spawn(async move {
            if let Err(error) = engine.start_and_handshake(&app).await {
                record(
                    &app,
                    Source::Host,
                    &format!("motor indisponível: {} ({})", error.message, error.code),
                );
                engine.set_status(&app, Status::Crashed);
                engine.discard_process();
            }
        });
    }

    /// Sem isto, um motor que não completou o handshake ficaria vivo, sem dono e com o estado da UI em `crashed`.
    fn discard_process(&self) {
        if let Some(child) = lock(&self.0.child).take() {
            let _ = child.kill();
        }
        lock(&self.0.pending).clear();
    }

    async fn start_and_handshake(&self, app: &AppHandle) -> Result<(), HostError> {
        self.set_status(app, Status::Starting);
        self.spawn_process(app)?;

        let hello = self
            .request(
                method::ENGINE_HELLO,
                json!({ "protocol": [PROTOCOL_VERSION], "hostVersion": env!("CARGO_PKG_VERSION") }),
            )
            .await?;

        if hello.get("protocol").and_then(Value::as_u64) != Some(PROTOCOL_VERSION) {
            return Err(HostError::new(
                error_code::UNSUPPORTED_PROTOCOL,
                "O motor não fala a versão de protocolo esperada.",
            ));
        }
        let version = hello
            .get("engineVersion")
            .and_then(Value::as_str)
            .unwrap_or("?");
        record(
            app,
            Source::Host,
            &format!("motor pronto (protocolo {PROTOCOL_VERSION}, versão {version})"),
        );
        self.set_status(app, Status::Ready);
        Ok(())
    }

    fn spawn_process(&self, app: &AppHandle) -> Result<(), HostError> {
        let mut command = app.shell().sidecar(SIDECAR).map_err(|_| {
            HostError::new(
                error_code::ENGINE_NOT_FOUND,
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
            HostError::new(
                error_code::ENGINE_START_FAILED,
                "Não foi possível iniciar o motor.",
            )
        })?;

        *lock(&self.0.child) = Some(child);

        let engine = self.clone();
        let app = app.clone();
        tauri::async_runtime::spawn(async move {
            while let Some(event) = rx.recv().await {
                match event {
                    CommandEvent::Stdout(line) => engine.on_stdout(&app, &line),
                    CommandEvent::Stderr(line) => {
                        record(
                            &app,
                            Source::Motor,
                            String::from_utf8_lossy(&line).trim_end(),
                        );
                    }
                    CommandEvent::Error(error) => record(
                        &app,
                        Source::Host,
                        &format!("erro no processo do motor: {error}"),
                    ),
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
            None => record(app, Source::Host, "linha descartada do stdout do motor"),
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
        record(
            app,
            Source::Host,
            &format!("o motor encerrou (código {code:?})"),
        );
        self.set_status(app, Status::Crashed);

        let uptime = lock(&self.0.ready_since)
            .take()
            .map(|since| since.elapsed());
        if restart_allowed(&self.0.restarts, uptime) {
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
                error_code::INVALID_MESSAGE,
                "Não foi possível serializar a requisição.",
            )
        })?;
        line.push(b'\n');

        {
            let mut guard = lock(&self.0.child);
            let Some(child) = guard.as_mut() else {
                lock(&self.0.pending).remove(&id);
                return Err(HostError::new(
                    error_code::ENGINE_NOT_RUNNING,
                    "O motor não está em execução.",
                ));
            };
            if child.write(&line).is_err() {
                lock(&self.0.pending).remove(&id);
                return Err(HostError::new(
                    error_code::ENGINE_WRITE_FAILED,
                    "Não foi possível falar com o motor.",
                ));
            }
        }

        let response = match tokio::time::timeout(request_timeout(method), rx).await {
            Ok(Ok(value)) => value,
            Ok(Err(_)) => {
                return Err(HostError::new(
                    error_code::ENGINE_STOPPED,
                    "O motor encerrou antes de responder.",
                ))
            }
            Err(_) => {
                lock(&self.0.pending).remove(&id);
                return Err(HostError::new(
                    error_code::ENGINE_TIMEOUT,
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
        response.get("result").cloned().ok_or_else(|| {
            HostError::new(
                error_code::INVALID_MESSAGE,
                "Resposta do motor sem resultado.",
            )
        })
    }

    /// Uma aplicação em andamento é cancelada e desfeita pelo motor antes de ele sair; só depois do prazo o processo é
    /// finalizado à força.
    pub fn shutdown(&self) {
        self.0.shutting_down.store(true, Ordering::SeqCst);
        self.0.terminated.store(false, Ordering::SeqCst);
        {
            let mut guard = lock(&self.0.child);
            let Some(child) = guard.as_mut() else { return };
            let mut line = serde_json::to_vec(&json!({
                "v": PROTOCOL_VERSION, "kind": "request", "id": "h-shutdown", "method": method::ENGINE_SHUTDOWN
            }))
            .unwrap_or_default();
            line.push(b'\n');
            let _ = child.write(&line);
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

/// Um motor que ficou estável por `STABLE_UPTIME` zera a conta, para uma queda esporádica horas depois também ser
/// recuperada.
fn restart_allowed(restarts: &AtomicU64, uptime: Option<Duration>) -> bool {
    if uptime.is_some_and(|up| up >= STABLE_UPTIME) {
        restarts.store(0, Ordering::SeqCst);
    }
    restarts.fetch_add(1, Ordering::SeqCst) < MAX_QUICK_RESTARTS
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn a_investigacao_espera_mais_que_o_prazo_do_motor_e_o_resto_o_prazo_comum() {
        // O motor dá 150 s à investigação (InvestigationService.Deadline): o host nunca pode desistir antes dele.
        assert!(request_timeout("management.investigate") > Duration::from_secs(150));
        assert_eq!(request_timeout("management.ask"), REQUEST_TIMEOUT);
        assert_eq!(request_timeout("run.snapshot"), REQUEST_TIMEOUT);
        assert!(request_timeout("iac.terraform.install") > Duration::from_secs(600));
    }

    #[test]
    fn queda_logo_apos_subir_tem_uma_tentativa_so() {
        let restarts = AtomicU64::new(0);
        assert!(restart_allowed(&restarts, Some(Duration::from_secs(2))));
        assert!(!restart_allowed(&restarts, Some(Duration::from_secs(2))));
        assert!(!restart_allowed(&restarts, None));
    }

    #[test]
    fn motor_estavel_por_um_tempo_ganha_nova_tentativa() {
        let restarts = AtomicU64::new(0);
        assert!(restart_allowed(&restarts, Some(Duration::from_secs(2))));
        assert!(!restart_allowed(&restarts, Some(Duration::from_secs(2))));
        assert!(restart_allowed(&restarts, Some(STABLE_UPTIME)));
        assert!(!restart_allowed(&restarts, Some(Duration::from_secs(1))));
    }
}
