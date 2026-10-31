mod engine;
mod protocol;
mod recents;

use std::path::PathBuf;
use std::sync::Mutex;
use std::time::{SystemTime, UNIX_EPOCH};

use serde_json::Value;
use tauri::{AppHandle, Manager, RunEvent, State};
use tauri_plugin_dialog::DialogExt;
use tokio::sync::oneshot;

use engine::Engine;
use protocol::{validate_frontend_request, HostError};
use recents::{project_id, RecentProject, RecentStore};

/// Lista de projetos recentes em memória, espelhada no diretório de dados do aplicativo.
struct Recents {
    store: Mutex<RecentStore>,
    file: PathBuf,
}

impl Recents {
    fn record(&self, path: &str, authorized: &Value) {
        let name = authorized.get("displayName").and_then(Value::as_str).unwrap_or("Projeto");
        let shown = authorized.get("displayPath").and_then(Value::as_str).unwrap_or("");
        let now = SystemTime::now().duration_since(UNIX_EPOCH).map(|d| d.as_millis() as u64).unwrap_or(0);

        let mut store = self.store.lock().unwrap();
        store.record(path, name, shown, now);
        // Perder o histórico não é uma falha da operação: só se registra.
        if let Err(error) = store.save(&self.file) {
            eprintln!("[host] não foi possível gravar os projetos recentes: {error}");
        }
    }
}

/// Repassa ao motor um pedido da UI, depois de validar o envelope e conferir a lista de métodos permitidos.
#[tauri::command]
async fn engine_request(engine: State<'_, Engine>, envelope: Value) -> Result<Value, HostError> {
    let (method, params) = validate_frontend_request(&envelope)?;
    engine.request(&method, params).await
}

/// Abre o diálogo nativo de pasta no lado Rust. O caminho nunca passa pela UI: o host o entrega
/// ao motor, que o autoriza, e a UI recebe só o nome e o caminho abreviado.
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

    let picked = rx
        .await
        .map_err(|_| HostError::new("DIALOG_FAILED", "O diálogo de seleção foi interrompido."))?;
    let Some(picked) = picked else {
        return Ok(None);
    };
    let path = picked
        .into_path()
        .map_err(|_| HostError::new("PATH_NOT_ALLOWED", "A pasta escolhida não pode ser usada."))?;
    let path_text = path.to_string_lossy().to_string();

    let result = engine.request("workspace.authorize", serde_json::json!({ "path": path_text })).await?;
    recents.record(&path_text, &result);
    Ok(Some(result))
}

/// Projetos escolhidos antes, do mais recente ao mais antigo. Sem caminho completo: só id, nome e caminho abreviado.
#[tauri::command]
fn list_recent_projects(recents: State<'_, Recents>) -> Vec<RecentProject> {
    recents.store.lock().unwrap().list(|path| std::path::Path::new(path).is_dir())
}

/// Reabre um projeto da lista, pelo id. A UI não envia caminho: só o que já passou pelo diálogo nativo pode voltar.
/// O motor autoriza o caminho de novo, com as mesmas regras de uma escolha nova.
#[tauri::command]
async fn open_recent_project(
    id: String,
    engine: State<'_, Engine>,
    recents: State<'_, Recents>,
) -> Result<Value, HostError> {
    let path = {
        let store = recents.store.lock().unwrap();
        store
            .find(&id)
            .map(|entry| entry.path.clone())
            .ok_or_else(|| HostError::new("RECENT_NOT_FOUND", "Este projeto não está mais na lista de recentes."))?
    };
    debug_assert_eq!(project_id(&path), id);

    if !std::path::Path::new(&path).is_dir() {
        return Err(HostError::new("PATH_NOT_FOUND", "A pasta deste projeto não existe mais ou não está acessível."));
    }

    let result = engine.request("workspace.authorize", serde_json::json!({ "path": path })).await?;
    recents.record(&path, &result);
    Ok(result)
}

#[tauri::command]
async fn engine_status(engine: State<'_, Engine>) -> Result<serde_json::Value, HostError> {
    let lifecycle = engine.status().as_str();
    if lifecycle != "ready" {
        return Ok(crate::engine::compose_status(lifecycle, None));
    }
    let health = engine.request("runtime.health", serde_json::json!({})).await.ok();
    Ok(crate::engine::compose_status(lifecycle, health.as_ref()))
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
            let file = app
                .path()
                .app_data_dir()
                .map(|dir| dir.join(recents::FILE_NAME))
                .unwrap_or_else(|_| std::env::temp_dir().join(recents::FILE_NAME));
            app.manage(Recents { store: Mutex::new(RecentStore::load(&file)), file });

            let engine = app.state::<Engine>().inner().clone();
            engine.boot(app.handle().clone());
            Ok(())
        })
        .invoke_handler(tauri::generate_handler![
            engine_request,
            pick_project_folder,
            engine_status,
            list_recent_projects,
            open_recent_project
        ])
        .build(tauri::generate_context!())
        .expect("falha ao iniciar o aplicativo Mariliz Studio")
        .run(|app, event| {
            if let RunEvent::Exit = event {
                app.state::<Engine>().shutdown();
            }
        });
}
