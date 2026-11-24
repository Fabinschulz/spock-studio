use tauri::ipc::Channel;
use tauri::State;

use crate::diagnostic_log::Diagnostics;
use crate::protocol::HostError;
use crate::recents::Recents;
use crate::terminal::{ChannelSink, TerminalEvent, TerminalOpened, Terminals};

/// Como em `open_recent_project`, a UI manda o id do recente e o host resolve a pasta: o terminal só abre num projeto
/// que passou pelo diálogo nativo.
#[tauri::command(async)]
pub fn terminal_open(
    recent_id: String,
    cols: u16,
    rows: u16,
    on_event: Channel<TerminalEvent>,
    recents: State<'_, Recents>,
    terminals: State<'_, Terminals>,
    diagnostics: State<'_, Diagnostics>,
) -> Result<TerminalOpened, HostError> {
    let dir = recents.existing_dir(&recent_id, "Abra o projeto de novo para usar o terminal.")?;
    terminals.open(
        &dir,
        cols,
        rows,
        ChannelSink::new(on_event, diagnostics.inner().clone()),
    )
}

// Os três abaixo são `async` (RS-14): síncronos, rodavam na thread principal, e o `taskkill` do fechar ou um PTY que não
// lê congelavam a janela.
#[tauri::command(async)]
pub fn terminal_write(
    session_id: String,
    data: String,
    terminals: State<'_, Terminals>,
) -> Result<(), HostError> {
    terminals.write(&session_id, &data)
}

#[tauri::command(async)]
pub fn terminal_resize(
    session_id: String,
    cols: u16,
    rows: u16,
    terminals: State<'_, Terminals>,
) -> Result<(), HostError> {
    terminals.resize(&session_id, cols, rows)
}

#[tauri::command(async)]
pub fn terminal_close(
    session_id: String,
    terminals: State<'_, Terminals>,
) -> Result<(), HostError> {
    terminals.close(&session_id)
}
