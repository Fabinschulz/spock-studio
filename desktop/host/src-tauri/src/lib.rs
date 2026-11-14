mod diagnostic_log;
mod engine;
mod protocol;
mod recents;
mod resources;
mod status;
mod terminal;

use std::sync::{Mutex, MutexGuard, PoisonError};

use serde_json::Value;
use tauri::ipc::Channel;
use tauri::{AppHandle, Manager, RunEvent, State};
use tauri_plugin_dialog::DialogExt;
use tokio::sync::oneshot;

use diagnostic_log::{DiagnosticLog, LogEntry};
use engine::Engine;
use protocol::{error_code, method, validate_frontend_request, HostError};
use recents::{AuthorizedProject, RecentProject, Recents};
use status::{compose_status, Status};
use terminal::{ChannelSink, TerminalEvent, TerminalOpened, Terminals};

/// Um mutex envenenado (uma thread entrou em pânico com ele travado) não derruba o host: o estado protegido aqui é
/// simples e continua utilizável.
pub(crate) fn lock<T>(mutex: &Mutex<T>) -> MutexGuard<'_, T> {
    mutex.lock().unwrap_or_else(PoisonError::into_inner)
}

#[tauri::command]
async fn engine_request(engine: State<'_, Engine>, envelope: Value) -> Result<Value, HostError> {
    let (method, params) = validate_frontend_request(&envelope)?;
    engine.request(&method, params).await
}

/// O `recentId` volta na resposta para a UI saber qual item da lista é o projeto atual.
async fn authorize_and_record(
    engine: &Engine,
    recents: &Recents,
    path: &str,
) -> Result<Value, HostError> {
    let mut authorized = engine
        .request(
            method::AUTHORIZE_WORKSPACE,
            serde_json::json!({ "path": path }),
        )
        .await?;
    let recent_id = recents.record(path, &AuthorizedProject::from_engine(&authorized));
    if let Some(fields) = authorized.as_object_mut() {
        fields.insert("recentId".into(), Value::String(recent_id));
    }
    Ok(authorized)
}

/// O caminho nunca passa pela UI: o host o entrega ao motor, que o autoriza, e a UI recebe só o nome e o caminho
/// abreviado.
#[tauri::command]
async fn pick_project_folder(
    app: AppHandle,
    engine: State<'_, Engine>,
    recents: State<'_, Recents>,
) -> Result<Option<Value>, HostError> {
    let (tx, rx) = oneshot::channel();
    app.dialog()
        .file()
        .set_title("Selecionar pasta do projeto")
        .pick_folder(move |picked| {
            let _ = tx.send(picked);
        });

    let picked = rx.await.map_err(|_| {
        HostError::new(
            error_code::DIALOG_FAILED,
            "O diálogo de seleção foi interrompido.",
        )
    })?;
    let Some(picked) = picked else {
        return Ok(None);
    };
    let path = picked.into_path().map_err(|_| {
        HostError::new(
            error_code::PATH_NOT_ALLOWED,
            "A pasta escolhida não pode ser usada.",
        )
    })?;
    let path_text = path.to_string_lossy().to_string();

    authorize_and_record(&engine, &recents, &path_text)
        .await
        .map(Some)
}

/// `async`: um comando síncrono roda na thread principal, e conferir as pastas pode demorar (caminho de rede desconectado).
#[tauri::command(async)]
fn list_recent_projects(recents: State<'_, Recents>) -> Vec<RecentProject> {
    recents.list()
}

/// A UI não envia caminho: só o que já passou pelo diálogo nativo pode voltar, e o motor o autoriza de novo com as
/// mesmas regras de uma escolha nova.
#[tauri::command]
async fn open_recent_project(
    id: String,
    engine: State<'_, Engine>,
    recents: State<'_, Recents>,
) -> Result<Value, HostError> {
    let path = recents.path_of(&id).ok_or_else(|| {
        HostError::new(
            error_code::RECENT_NOT_FOUND,
            "Este projeto não está mais na lista de recentes.",
        )
    })?;

    let to_check = path.clone();
    let exists =
        tauri::async_runtime::spawn_blocking(move || std::path::Path::new(&to_check).is_dir())
            .await
            .unwrap_or(false);
    if !exists {
        return Err(HostError::new(
            error_code::PATH_NOT_FOUND,
            "A pasta deste projeto não existe mais ou não está acessível.",
        ));
    }

    authorize_and_record(&engine, &recents, &path).await
}

#[tauri::command]
async fn engine_status(engine: State<'_, Engine>) -> Result<serde_json::Value, HostError> {
    let lifecycle = engine.status();
    if lifecycle != Status::Ready {
        return Ok(compose_status(lifecycle, None));
    }
    let health = engine
        .request(method::RUNTIME_HEALTH, serde_json::json!({}))
        .await
        .ok();
    Ok(compose_status(lifecycle, health.as_ref()))
}

#[tauri::command]
fn diagnostic_log(log: State<'_, DiagnosticLog>) -> Vec<LogEntry> {
    log.snapshot()
}

/// Como em `open_recent_project`, a UI manda o id do recente e o host resolve a pasta: o terminal só abre num projeto
/// que passou pelo diálogo nativo.
#[tauri::command(async)]
fn terminal_open(
    app: AppHandle,
    recent_id: String,
    cols: u16,
    rows: u16,
    on_event: Channel<TerminalEvent>,
    recents: State<'_, Recents>,
    terminals: State<'_, Terminals>,
) -> Result<TerminalOpened, HostError> {
    let path = recents.path_of(&recent_id).ok_or_else(|| {
        HostError::new(
            error_code::RECENT_NOT_FOUND,
            "Abra o projeto de novo para usar o terminal.",
        )
    })?;
    let dir = std::path::PathBuf::from(path);
    if !dir.is_dir() {
        return Err(HostError::new(
            error_code::PATH_NOT_FOUND,
            "A pasta deste projeto não existe mais ou não está acessível.",
        ));
    }
    terminals.open(&dir, cols, rows, ChannelSink::new(on_event, app))
}

#[tauri::command]
fn terminal_write(
    session_id: String,
    data: String,
    terminals: State<'_, Terminals>,
) -> Result<(), HostError> {
    terminals.write(&session_id, &data)
}

#[tauri::command]
fn terminal_resize(
    session_id: String,
    cols: u16,
    rows: u16,
    terminals: State<'_, Terminals>,
) -> Result<(), HostError> {
    terminals.resize(&session_id, cols, rows)
}

#[tauri::command]
fn terminal_close(session_id: String, terminals: State<'_, Terminals>) -> Result<(), HostError> {
    terminals.close(&session_id)
}

pub fn run() {
    tauri::Builder::default()
        .plugin(tauri_plugin_single_instance::init(|app, _args, _cwd| {
            if let Some(window) = app.get_webview_window("main") {
                let _ = window.unminimize();
                let _ = window.set_focus();
            }
        }))
        .plugin(tauri_plugin_dialog::init())
        .plugin(tauri_plugin_shell::init())
        .manage(Engine::default())
        .manage(DiagnosticLog::default())
        .manage(Terminals::default())
        .setup(|app| {
            let file = app
                .path()
                .app_data_dir()
                .map(|dir| dir.join(recents::FILE_NAME))
                .unwrap_or_else(|_| std::env::temp_dir().join(recents::FILE_NAME));
            app.manage(Recents::load(file));

            let engine = app.state::<Engine>().inner().clone();
            engine.boot(app.handle().clone());
            Ok(())
        })
        .invoke_handler(tauri::generate_handler![
            engine_request,
            pick_project_folder,
            engine_status,
            list_recent_projects,
            open_recent_project,
            diagnostic_log,
            terminal_open,
            terminal_write,
            terminal_resize,
            terminal_close
        ])
        .build(tauri::generate_context!())
        .expect("falha ao iniciar o aplicativo Spock Studio")
        .run(|app, event| {
            if let RunEvent::Exit = event {
                app.state::<Terminals>().close_all();
                app.state::<Engine>().shutdown();
            }
        });
}
