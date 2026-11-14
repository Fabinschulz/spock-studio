use std::path::{Path, PathBuf};

pub struct Shell {
    pub program: PathBuf,
    pub args: Vec<String>,
    pub label: String,
}

/// No Windows, o PowerShell 7 quando instalado e, sem ele, o Windows PowerShell, que sempre existe. Nos outros
/// sistemas, o shell da pessoa como shell de login, para carregar o perfil dela.
pub fn default_shell() -> Shell {
    if cfg!(windows) {
        let found = find_in_path("pwsh.exe");
        let program = found.unwrap_or_else(|| PathBuf::from("powershell.exe"));
        let label = if program.ends_with("pwsh.exe") {
            "PowerShell"
        } else {
            "Windows PowerShell"
        };
        return Shell {
            program,
            args: vec!["-NoLogo".into()],
            label: label.into(),
        };
    }

    let program = std::env::var_os("SHELL")
        .map(PathBuf::from)
        .filter(|path| path.is_absolute() && path.is_file())
        .unwrap_or_else(|| PathBuf::from("/bin/sh"));
    let label = program
        .file_name()
        .map(|name| name.to_string_lossy().into_owned())
        .unwrap_or_else(|| "shell".into());
    Shell {
        program,
        args: vec!["-l".into()],
        label,
    }
}

fn find_in_path(name: &str) -> Option<PathBuf> {
    let path = std::env::var_os("PATH")?;
    std::env::split_paths(&path)
        .map(|dir| dir.join(name))
        .find(|candidate| Path::new(candidate).is_file())
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn sempre_ha_um_shell_com_nome_para_mostrar() {
        let shell = default_shell();

        assert!(!shell.label.is_empty());
        assert!(!shell.program.as_os_str().is_empty());
    }
}
