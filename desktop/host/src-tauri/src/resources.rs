use std::path::{Path, PathBuf};

use serde::Serialize;

/// Os avisos de terceiros que o instalador leva em `notices/` (gerados por docs/compliance/tools/generate_sbom.py).
#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct Notices {
    pub text: String,
    pub components: u64,
    pub complete: bool,
}

pub fn third_party_notices(resource_dir: &Path) -> Option<Notices> {
    let dir = resource_dir.join("notices");
    let text = std::fs::read_to_string(dir.join("THIRD_PARTY_NOTICES.txt")).ok()?;
    let manifest = std::fs::read_to_string(dir.join("notices-manifest.json"))
        .ok()
        .and_then(|raw| serde_json::from_str::<serde_json::Value>(&raw).ok())
        .unwrap_or_default();
    Some(Notices {
        text,
        components: manifest["components"].as_u64().unwrap_or(0),
        complete: manifest["complete"].as_bool().unwrap_or(false),
    })
}

pub fn packaged_runtime(resource_dir: &Path) -> (Option<PathBuf>, Option<PathBuf>) {
    let script = resource_dir.join("intelligence").join("server.py");
    let python = if cfg!(windows) {
        resource_dir.join("python").join("python.exe")
    } else {
        resource_dir.join("python").join("bin").join("python3")
    };
    (
        script.is_file().then_some(script),
        python.is_file().then_some(python),
    )
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn avisos_vem_do_texto_e_do_manifesto_e_faltam_sem_o_texto() {
        let dir = std::env::temp_dir().join(format!("mariliz-notices-{}", std::process::id()));
        let _ = std::fs::remove_dir_all(&dir);
        assert!(third_party_notices(&dir).is_none());

        std::fs::create_dir_all(dir.join("notices")).unwrap();
        std::fs::write(
            dir.join("notices").join("THIRD_PARTY_NOTICES.txt"),
            "MIT License",
        )
        .unwrap();
        let sem_manifesto = third_party_notices(&dir).unwrap();
        assert_eq!(sem_manifesto.text, "MIT License");
        assert!(!sem_manifesto.complete);

        std::fs::write(
            dir.join("notices").join("notices-manifest.json"),
            r#"{"components": 484, "complete": true}"#,
        )
        .unwrap();
        let avisos = third_party_notices(&dir).unwrap();
        assert_eq!(avisos.components, 484);
        assert!(avisos.complete);
        let _ = std::fs::remove_dir_all(&dir);
    }

    #[test]
    fn recursos_empacotados_so_existem_quando_os_arquivos_existem() {
        let dir = std::env::temp_dir().join(format!("mariliz-res-{}", std::process::id()));
        let _ = std::fs::remove_dir_all(&dir);
        std::fs::create_dir_all(dir.join("intelligence")).unwrap();
        std::fs::write(dir.join("intelligence").join("server.py"), b"print('ok')").unwrap();
        let (script, python) = packaged_runtime(&dir);
        assert!(script.is_some());
        assert!(python.is_none());

        let python_file = if cfg!(windows) {
            dir.join("python").join("python.exe")
        } else {
            dir.join("python").join("bin").join("python3")
        };
        std::fs::create_dir_all(python_file.parent().unwrap()).unwrap();
        std::fs::write(&python_file, b"").unwrap();
        let (script, python) = packaged_runtime(&dir);
        assert!(script.is_some());
        assert!(python.is_some());
        let _ = std::fs::remove_dir_all(&dir);
    }
}
