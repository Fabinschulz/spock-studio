//! O processo do motor: subir o sidecar com o runtime de IA empacotado e encerrá-lo. A escrita no stdin é de uma
//! thread só (`Writer`), dona do `CommandChild`.

use std::time::Duration;

use tauri::async_runtime::Receiver;
use tauri::{AppHandle, Manager};
use tauri_plugin_shell::process::{CommandChild, CommandEvent};
use tauri_plugin_shell::ShellExt;

use crate::protocol::{error_code, HostError};
use crate::resources::packaged_runtime;
use crate::writer::{Output, Writer};

const SIDECAR: &str = "mariliz-runtime";
// Com o stdin travado, a ordem de encerrar fica na fila atrás da escrita presa; passado este prazo, o encerramento vai
// pelo id do processo.
const KILL_WAIT: Duration = Duration::from_secs(2);

pub struct Process {
    pub writer: Writer,
    pub pid: u32,
}

struct ChildInput(CommandChild);

impl Output for ChildInput {
    fn write(&mut self, data: &[u8]) -> bool {
        self.0.write(data).is_ok()
    }

    fn finish(self) {
        let _ = self.0.kill();
    }
}

pub fn spawn(app: &AppHandle) -> Result<(Process, Receiver<CommandEvent>), HostError> {
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
    let (events, child) = command.spawn().map_err(|_| {
        HostError::new(
            error_code::ENGINE_START_FAILED,
            "Não foi possível iniciar o motor.",
        )
    })?;
    let pid = child.pid();
    Ok((
        Process {
            writer: Writer::spawn(ChildInput(child)),
            pid,
        },
        events,
    ))
}

impl Process {
    /// Não bloqueia quem chama; quem precisa esperar (o encerramento do aplicativo) usa o `JoinHandle`.
    pub fn kill(self) -> std::thread::JoinHandle<()> {
        let closed = self.writer.close();
        let pid = self.pid;
        std::thread::spawn(move || {
            if closed.recv_timeout(KILL_WAIT).is_err() {
                force_kill(pid);
            }
        })
    }
}

#[cfg(windows)]
fn force_kill(pid: u32) {
    use std::os::windows::process::CommandExt;
    const CREATE_NO_WINDOW: u32 = 0x0800_0000;
    // /T leva junto o runtime de IA que o motor subiu.
    let _ = std::process::Command::new("taskkill")
        .args(["/PID", &pid.to_string(), "/T", "/F"])
        .creation_flags(CREATE_NO_WINDOW)
        .status();
}

#[cfg(not(windows))]
fn force_kill(pid: u32) {
    // 0 e 1 seriam o grupo do próprio Spock e o init.
    if pid <= 1 {
        return;
    }
    let _ = std::process::Command::new("kill")
        .args(["-KILL", "--", &pid.to_string()])
        .status();
}
