fn main() {
    // Só estes comandos existem para o frontend; cada um exige a permissão `allow-<nome>`
    // declarada em capabilities/default.json.
    let manifest = tauri_build::AppManifest::new().commands(&[
        "engine_request",
        "pick_project_folder",
        "engine_status",
    ]);

    tauri_build::try_build(tauri_build::Attributes::new().app_manifest(manifest))
        .expect("falha ao gerar o manifesto de permissões do aplicativo");
}
