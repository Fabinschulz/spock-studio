mod engine;
mod protocol;

use serde_json::Value;
use tauri::{AppHandle, Manager, RunEvent, State};
use tauri_plugin_dialog::DialogExt;
use tokio::sync::oneshot;

use engine::Engine;
use protocol::{validate_frontend_request, HostError};

/// Repassa ao motor um pedido da UI, depois de validar o envelope e conferir a lista de métodos permitidos.
#[tauri::command]
async fn engine_request(engine: State<'_, Engine>, envelope: Value) -> Result<Value, HostError> {
    let (method, params) = validate_frontend_request(&envelope)?;
    engine.request(&method, params).await
}

/// Abre o diálogo nativo de pasta no lado Rust. O caminho nunca passa pela UI: o host o entrega
/// ao motor, que o autoriza, e a UI recebe só o nome e o caminho abreviado.
#[tauri::command]
async fn pick_project_folder(app: AppHandle, engine: State<'_, Engine>) -> Result<Option<Value>, HostError> {
    let (tx, rx) = oneshot::channel();
    app.dialog()
        .file()
        .set_title("Selecionar pasta do projeto")
        .pick_folder(move |picked| {
            let _ = tx.send(picked);
        });

    let picked = rx
        .await
        .map_err(|_| HostError::new("DIALOG_FAILED", "O diálogo de seleção foi interrompido."))?;
    let Some(picked) = picked else {
        return Ok(None);
    };
    let path = picked
        .into_path()
        .map_err(|_| HostError::new("PATH_NOT_ALLOWED", "A pasta escolhida não pode ser usada."))?;

    let result = engine
        .request("workspace.authorize", serde_json::json!({ "path": path.to_string_lossy() }))
        .await?;
    Ok(Some(result))
}

#[tauri::command]
fn engine_status(engine: State<'_, Engine>) -> &'static str {
    engine.status().as_str()
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
        .setup(|app| {
            let engine = app.state::<Engine>().inner().clone();
            engine.boot(app.handle().clone());
            Ok(())
        })
        .invoke_handler(tauri::generate_handler![engine_request, pick_project_folder, engine_status])
        .build(tauri::generate_context!())
        .expect("falha ao iniciar o aplicativo Mariliz AI Engineering")
        .run(|app, event| {
            if let RunEvent::Exit = event {
                app.state::<Engine>().shutdown();
            }
        });
}
