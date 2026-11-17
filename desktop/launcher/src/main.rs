//! `spock`: o comando estável que os arquivos de IDE versionados chamam (`spock mcp --project-dir …`,
//! `spock context session`), em vez do caminho absoluto do motor, que muda de máquina para máquina (DD-6).
//!
//! Não tem regra nenhuma: acha o `mariliz-runtime` instalado junto e repassa argumentos, entrada, saída e código de saída.
//! A entrada e a saída são herdadas, então o MCP por stdio funciona através dele.

use std::env;
use std::path::{Path, PathBuf};
use std::process::{Command, ExitCode};

const RUNTIME: &str = if cfg!(windows) {
    "mariliz-runtime.exe"
} else {
    "mariliz-runtime"
};

// O instalador do Windows põe uma cópia em `bin\` (a pasta que vai para o PATH, sem expor os outros executáveis); no
// Linux os dois ficam em /usr/bin.
fn runtime_for(launcher: &Path) -> Option<PathBuf> {
    let dir = launcher.parent()?;
    let mut candidates = vec![dir.join(RUNTIME)];
    if let Some(parent) = dir.parent() {
        candidates.push(parent.join(RUNTIME));
    }
    candidates.into_iter().find(|candidate| candidate.is_file())
}

fn main() -> ExitCode {
    let runtime = env::current_exe()
        .ok()
        .and_then(|launcher| runtime_for(&launcher));
    let Some(runtime) = runtime else {
        eprintln!(
            "spock: o motor ({RUNTIME}) não foi encontrado junto ao lançador. Reinstale o Spock Studio."
        );
        return ExitCode::from(127);
    };
    match Command::new(&runtime).args(env::args_os().skip(1)).status() {
        Ok(status) => ExitCode::from(status.code().map_or(1, |code| code.clamp(0, 255) as u8)),
        Err(error) => {
            eprintln!(
                "spock: não foi possível iniciar o motor em {}: {error}",
                runtime.display()
            );
            ExitCode::from(126)
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::fs;

    fn temp_dir(name: &str) -> PathBuf {
        let dir = env::temp_dir().join(format!("spock-launcher-{name}-{}", std::process::id()));
        let _ = fs::remove_dir_all(&dir);
        fs::create_dir_all(dir.join("bin")).unwrap();
        dir
    }

    #[test]
    fn acha_o_motor_na_mesma_pasta() {
        let dir = temp_dir("mesma");
        fs::write(dir.join("bin").join(RUNTIME), b"").unwrap();

        assert_eq!(
            runtime_for(&dir.join("bin").join("spock")),
            Some(dir.join("bin").join(RUNTIME))
        );
        fs::remove_dir_all(dir).unwrap();
    }

    #[test]
    fn acha_o_motor_na_pasta_de_cima_quando_o_lancador_esta_em_bin() {
        let dir = temp_dir("acima");
        fs::write(dir.join(RUNTIME), b"").unwrap();

        assert_eq!(
            runtime_for(&dir.join("bin").join("spock")),
            Some(dir.join(RUNTIME))
        );
        fs::remove_dir_all(dir).unwrap();
    }

    #[test]
    fn sem_motor_nao_inventa_caminho() {
        let dir = temp_dir("sem");

        assert_eq!(runtime_for(&dir.join("bin").join("spock")), None);
        fs::remove_dir_all(dir).unwrap();
    }
}
