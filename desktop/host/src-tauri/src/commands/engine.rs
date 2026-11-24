use serde_json::Value;
use tauri::State;

use crate::engine::Engine;
use crate::protocol::{method, validate_frontend_request, HostError};
use crate::status::{compose_status, Status};

#[tauri::command]
pub async fn engine_request(
    engine: State<'_, Engine>,
    envelope: Value,
) -> Result<Value, HostError> {
    let (method, params) = validate_frontend_request(&envelope)?;
    engine.request(&method, params).await
}

#[tauri::command]
pub async fn engine_status(engine: State<'_, Engine>) -> Result<Value, HostError> {
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
