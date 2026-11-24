use tauri::{AppHandle, Manager, State};

use crate::diagnostic_log::{Diagnostics, LogEntry};
use crate::protocol::{error_code, HostError};
use crate::resources;

#[tauri::command]
pub fn diagnostic_log(diagnostics: State<'_, Diagnostics>) -> Vec<LogEntry> {
    diagnostics.snapshot()
}

/// Avisos de terceiros que o instalador leva como recurso (D-07, docs/compliance), para a tela Sobre.
#[tauri::command(async)]
pub fn third_party_notices(app: AppHandle) -> Result<resources::Notices, HostError> {
    app.path()
        .resource_dir()
        .ok()
        .and_then(|dir| resources::third_party_notices(&dir))
        .ok_or_else(|| {
            HostError::new(
                error_code::NOTICES_NOT_FOUND,
                "Os avisos de terceiros não estão nesta instalação.",
            )
        })
}
