//! Recursos que o instalador empacota junto do host.

use std::path::{Path, PathBuf};

/// Caminhos do interpretador e do script empacotados como recursos do instalador.
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
