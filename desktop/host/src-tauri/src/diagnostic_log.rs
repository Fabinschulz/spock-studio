//! As últimas linhas que o motor e o host escreveram, para a tela de Diagnóstico. Segredos e caminhos são redigidos
//! antes de guardar: a UI nunca recebe o caminho completo de um projeto, e uma linha de log não é exceção.

use std::collections::VecDeque;
use std::sync::{Arc, Mutex};
use std::time::{SystemTime, UNIX_EPOCH};

use serde::Serialize;

use crate::lock;
use crate::terminal::is_secret_variable;

const CAPACITY: usize = 300;
const MAX_MESSAGE_CHARS: usize = 400;
const REDACTED_SECRET: &str = "[segredo]";
const REDACTED_PATH: &str = "[caminho]";

#[derive(Debug, Clone, Copy, PartialEq, Serialize)]
#[serde(rename_all = "lowercase")]
pub enum Source {
    Motor,
    Host,
}

impl Source {
    fn label(self) -> &'static str {
        match self {
            Source::Motor => "motor",
            Source::Host => "host",
        }
    }
}

#[derive(Debug, Clone, PartialEq, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct LogEntry {
    pub at_ms: u64,
    pub source: Source,
    pub message: String,
}

#[derive(Default)]
struct DiagnosticLog {
    entries: Mutex<VecDeque<LogEntry>>,
}

impl DiagnosticLog {
    fn push(&self, source: Source, message: &str) {
        let mut entries = lock(&self.entries);
        if entries.len() == CAPACITY {
            entries.pop_front();
        }
        entries.push_back(LogEntry {
            at_ms: now_ms(),
            source,
            message: redact(message),
        });
    }

    fn snapshot(&self) -> Vec<LogEntry> {
        lock(&self.entries).iter().cloned().collect()
    }
}

/// O único canal de log do host: escreve no stderr e guarda a linha redigida para a tela. Quem registra recebe um clone
/// na criação, e o teste lê o que foi registrado sem subir o Tauri.
#[derive(Clone, Default)]
pub struct Diagnostics(Arc<DiagnosticLog>);

impl Diagnostics {
    pub fn host(&self, message: &str) {
        self.record(Source::Host, message);
    }

    pub fn motor(&self, message: &str) {
        self.record(Source::Motor, message);
    }

    fn record(&self, source: Source, message: &str) {
        eprintln!("[{}] {message}", source.label());
        self.0.push(source, message);
    }

    pub fn snapshot(&self) -> Vec<LogEntry> {
        self.0.snapshot()
    }
}

fn now_ms() -> u64 {
    SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .map(|elapsed| elapsed.as_millis() as u64)
        .unwrap_or(0)
}

fn is_secret(word: &str) -> bool {
    let token = word.trim_matches(|c: char| !c.is_ascii_alphanumeric() && c != '-' && c != '_');
    token.starts_with("sk-")
        || (token.len() >= 32
            && token
                .chars()
                .all(|c| c.is_ascii_alphanumeric() || c == '-' || c == '_'))
}

fn is_path(word: &str) -> bool {
    let drive =
        word.len() > 2 && word.as_bytes()[1] == b':' && matches!(word.as_bytes()[2], b'\\' | b'/');
    drive || word.starts_with("\\\\") || (word.starts_with('/') && word[1..].contains('/'))
}

fn redact_word(word: &str, after_credential: bool) -> String {
    if after_credential || is_secret(word) {
        REDACTED_SECRET.to_string()
    } else if let Some((name, value)) = word.split_once('=') {
        // A regra do ambiente do terminal (a mesma do motor), mais nomes que num log costumam guardar segredo.
        let lowered = name.to_lowercase();
        let sensitive = is_secret_variable(name)
            || ["key", "senha"]
                .iter()
                .any(|marker| lowered.contains(marker));
        if sensitive {
            format!("{name}={REDACTED_SECRET}")
        } else if is_path(value) {
            format!("{name}={REDACTED_PATH}")
        } else {
            word.to_string()
        }
    } else if is_path(word) {
        REDACTED_PATH.to_string()
    } else {
        word.to_string()
    }
}

/// Redige por palavra: uma chave, o valor depois de `Bearer` e `nome=valor` sensível viram `[segredo]`; um caminho
/// absoluto vira `[caminho]`. A linha é cortada em um tamanho máximo.
pub fn redact(message: &str) -> String {
    let mut after_credential = false;
    let words: Vec<String> = message
        .split_whitespace()
        .map(|word| {
            let out = redact_word(word, after_credential);
            after_credential = word.eq_ignore_ascii_case("bearer");
            out
        })
        .collect();

    words.join(" ").chars().take(MAX_MESSAGE_CHARS).collect()
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn chave_de_provedor_vira_segredo() {
        assert_eq!(
            redact("falhou com sk-abc123def456ghi789 no envio"),
            "falhou com [segredo] no envio"
        );
    }

    #[test]
    fn valor_depois_de_bearer_vira_segredo() {
        assert_eq!(
            redact("Authorization: Bearer abc.def"),
            "Authorization: Bearer [segredo]"
        );
    }

    #[test]
    fn valor_sensivel_em_nome_igual_valor_vira_segredo() {
        assert_eq!(
            redact("api_key=meuvalor token=x ok=1"),
            "api_key=[segredo] token=[segredo] ok=1"
        );
    }

    #[test]
    fn nome_que_o_terminal_esconde_tambem_vira_segredo_no_log() {
        assert_eq!(
            redact("ConnectionStrings__Default=Server=x;Password=p credential=c passwd=p"),
            "ConnectionStrings__Default=[segredo] credential=[segredo] passwd=[segredo]"
        );
    }

    #[test]
    fn texto_longo_sem_espaco_parecido_com_chave_vira_segredo() {
        let token = "a".repeat(40);
        assert_eq!(redact(&format!("id {token}")), "id [segredo]");
    }

    #[test]
    fn caminho_absoluto_vira_caminho() {
        assert_eq!(
            redact(r"abrindo C:\Users\ana\loja agora"),
            "abrindo [caminho] agora"
        );
        assert_eq!(
            redact("abrindo /home/ana/loja agora"),
            "abrindo [caminho] agora"
        );
        assert_eq!(redact("pasta=/home/ana/loja"), "pasta=[caminho]");
    }

    #[test]
    fn texto_comum_passa_sem_mudar() {
        assert_eq!(
            redact("o motor encerrou (código Some(1))"),
            "o motor encerrou (código Some(1))"
        );
    }

    #[test]
    fn a_linha_e_cortada() {
        assert_eq!(
            redact(&"palavra ".repeat(200)).chars().count(),
            MAX_MESSAGE_CHARS
        );
    }

    #[test]
    fn os_clones_escrevem_no_mesmo_registro_e_a_linha_chega_redigida() {
        let diagnostics = Diagnostics::default();
        let engine = diagnostics.clone();

        engine.motor("subiu");
        diagnostics.host(r"falhou em C:\Users\ana\loja");

        let entries = diagnostics.snapshot();
        assert_eq!(
            entries
                .iter()
                .map(|entry| (entry.source, entry.message.as_str()))
                .collect::<Vec<_>>(),
            [
                (Source::Motor, "subiu"),
                (Source::Host, "falhou em [caminho]")
            ]
        );
    }

    #[test]
    fn guarda_so_as_ultimas_linhas() {
        let log = DiagnosticLog::default();
        for number in 0..(CAPACITY + 5) {
            log.push(Source::Motor, &format!("linha {number}"));
        }

        let entries = log.snapshot();
        assert_eq!(entries.len(), CAPACITY);
        assert_eq!(
            entries.first().map(|entry| entry.message.as_str()),
            Some("linha 5")
        );
        assert_eq!(
            entries.last().map(|entry| entry.message.as_str()),
            Some(format!("linha {}", CAPACITY + 4).as_str())
        );
    }
}
