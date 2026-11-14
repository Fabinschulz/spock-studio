//! O shell do terminal herda o ambiente do aplicativo, menos o que parece segredo: um comando colado de um site não
//! encontra a chave de uma ferramenta esquecida numa variável. Quem precisa de credencial usa o login da própria
//! ferramenta (gh auth login, az login...), que guarda o segredo fora do ambiente.

const SECRET_MARKERS: &[&str] = &[
    "TOKEN",
    "SECRET",
    "PASSWORD",
    "PASSWD",
    "API_KEY",
    "APIKEY",
    "PRIVATE_KEY",
    "CREDENTIAL",
    "ACCESS_KEY",
    "CLIENT_KEY",
    "SESSION_KEY",
    "CONNECTION_STRING",
    // O .NET lê `ConnectionStrings__Default` do ambiente, sem sublinhado entre as palavras.
    "CONNECTIONSTRING",
];

pub fn is_secret(name: &str) -> bool {
    let upper = name.to_uppercase();
    SECRET_MARKERS.iter().any(|marker| upper.contains(marker))
}

pub fn shell_env(inherited: impl Iterator<Item = (String, String)>) -> Vec<(String, String)> {
    inherited
        .filter(|(name, _)| !is_secret(name) && name != "TERM" && name != "COLORTERM")
        .chain([
            ("TERM".to_string(), "xterm-256color".to_string()),
            ("COLORTERM".to_string(), "truecolor".to_string()),
        ])
        .collect()
}

#[cfg(test)]
mod tests {
    use super::*;

    fn names(vars: &[(&str, &str)]) -> Vec<String> {
        shell_env(vars.iter().map(|(k, v)| (k.to_string(), v.to_string())))
            .into_iter()
            .map(|(name, _)| name)
            .collect()
    }

    #[test]
    fn variaveis_com_cara_de_segredo_ficam_de_fora() {
        let kept = names(&[
            ("GITHUB_TOKEN", "x"),
            ("OPENAI_API_KEY", "x"),
            ("AWS_SECRET_ACCESS_KEY", "x"),
            ("DB_PASSWORD", "x"),
            ("Azure_Client_Secret", "x"),
            ("ConnectionStrings__Default", "x"),
            ("MY_CONNECTION_STRING", "x"),
        ]);

        assert_eq!(kept, ["TERM", "COLORTERM"]);
    }

    #[test]
    fn o_resto_do_ambiente_passa_para_as_ferramentas_funcionarem() {
        let kept = names(&[
            ("PATH", "C:\\bin"),
            ("SystemRoot", "C:\\Windows"),
            ("JAVA_HOME", "C:\\jdk"),
            ("NVM_HOME", "C:\\nvm"),
            ("USERPROFILE", "C:\\Users\\x"),
        ]);

        assert_eq!(
            kept,
            [
                "PATH",
                "SystemRoot",
                "JAVA_HOME",
                "NVM_HOME",
                "USERPROFILE",
                "TERM",
                "COLORTERM"
            ]
        );
    }

    #[test]
    fn o_tipo_de_terminal_e_sempre_o_que_a_tela_entende() {
        let env = shell_env([("TERM".to_string(), "dumb".to_string())].into_iter());

        assert_eq!(env.iter().filter(|(name, _)| name == "TERM").count(), 1);
        assert!(env.contains(&("TERM".to_string(), "xterm-256color".to_string())));
    }
}
