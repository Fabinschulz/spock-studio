use serde_json::Value;
use tauri::{AppHandle, State};
use tauri_plugin_dialog::DialogExt;
use tokio::sync::oneshot;

use crate::engine::{self, Engine};
use crate::protocol::{error_code, method, HostError};
use crate::recents::{missing_dir, AuthorizedProject, RecentProject, Recents};

/// O `recentId` volta na resposta para a UI saber qual item da lista é o projeto atual.
async fn authorize_and_record(
    engine: &Engine,
    recents: &Recents,
    path: &str,
) -> Result<Value, HostError> {
    engine.wait_until_started(engine::STARTUP_WAIT).await;
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
pub async fn pick_project_folder(
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
pub fn list_recent_projects(recents: State<'_, Recents>) -> Vec<RecentProject> {
    recents.list()
}

/// A UI não envia caminho: só o que já passou pelo diálogo nativo pode voltar, e o motor o autoriza de novo com as
/// mesmas regras de uma escolha nova.
#[tauri::command]
pub async fn open_recent_project(
    id: String,
    engine: State<'_, Engine>,
    recents: State<'_, Recents>,
) -> Result<Value, HostError> {
    let lookup = recents.inner().clone();
    let dir = tauri::async_runtime::spawn_blocking(move || {
        lookup.existing_dir(&id, "Este projeto não está mais na lista de recentes.")
    })
    .await
    .unwrap_or_else(|_| Err(missing_dir()))?;

    authorize_and_record(&engine, &recents, &dir.to_string_lossy()).await
}
