mod commands;
mod diagnostic_log;
mod engine;
mod protocol;
mod recents;
mod resources;
mod status;
mod terminal;
mod writer;

use std::sync::{Mutex, MutexGuard, PoisonError};

use tauri::{Manager, RunEvent};

use diagnostic_log::Diagnostics;
use engine::Engine;
use recents::Recents;
use terminal::Terminals;

/// Um mutex envenenado (uma thread entrou em pânico com ele travado) não derruba o host: o estado protegido aqui é
/// simples e continua utilizável.
pub(crate) fn lock<T>(mutex: &Mutex<T>) -> MutexGuard<'_, T> {
    mutex.lock().unwrap_or_else(PoisonError::into_inner)
}

pub fn run() {
    let diagnostics = Diagnostics::default();
    tauri::Builder::default()
        .plugin(tauri_plugin_single_instance::init(|app, _args, _cwd| {
            if let Some(window) = app.get_webview_window("main") {
                let _ = window.unminimize();
                let _ = window.set_focus();
            }
        }))
        .plugin(tauri_plugin_dialog::init())
        .plugin(tauri_plugin_shell::init())
        .manage(Engine::new(diagnostics.clone()))
        .manage(diagnostics.clone())
        .manage(Terminals::default())
        .setup(move |app| {
            let file = app
                .path()
                .app_data_dir()
                .map(|dir| dir.join(recents::FILE_NAME))
                .unwrap_or_else(|_| std::env::temp_dir().join(recents::FILE_NAME));
            app.manage(Recents::load(file, diagnostics));

            let engine = app.state::<Engine>().inner().clone();
            engine.boot(app.handle().clone());
            Ok(())
        })
        .invoke_handler(tauri::generate_handler![
            commands::engine::engine_request,
            commands::engine::engine_status,
            commands::engine::restart_engine,
            commands::projects::pick_project_folder,
            commands::projects::list_recent_projects,
            commands::projects::open_recent_project,
            commands::about::diagnostic_log,
            commands::about::third_party_notices,
            commands::terminal::terminal_open,
            commands::terminal::terminal_write,
            commands::terminal::terminal_resize,
            commands::terminal::terminal_close
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
