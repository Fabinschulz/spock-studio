//! Terminal do projeto. Só abre na pasta de um projeto que passou pelo diálogo nativo e foi autorizado (a UI manda o id
//! do recente, nunca um caminho), sem as variáveis de ambiente com cara de segredo, com no máximo `MAX_SESSIONS`
//! sessões, e fechar uma sessão encerra a árvore inteira de processos, não só o shell. O registro de diagnóstico guarda
//! quando cada sessão abre e fecha, nunca o que foi digitado ou mostrado.

mod env;
mod shell;
mod utf8;

pub use env::is_secret as is_secret_variable;

use std::collections::HashMap;
use std::io::{Read, Write};
use std::path::Path;
use std::sync::atomic::{AtomicU64, Ordering};
use std::sync::mpsc::{self, Receiver};
use std::sync::{Arc, Mutex};
use std::time::Duration;

use portable_pty::{native_pty_system, Child, CommandBuilder, MasterPty, PtySize};
use serde::Serialize;
use tauri::ipc::Channel;
use tauri::AppHandle;

use crate::diagnostic_log::{record, Source};
use crate::lock;
use crate::protocol::{error_code, HostError};
use crate::writer::{Output, Writer};

pub const MAX_SESSIONS: usize = 4;
pub const MAX_WRITE_BYTES: usize = 64 * 1024;
const READ_CHUNK: usize = 8 * 1024;
const EXIT_POLL: Duration = Duration::from_millis(150);
const LAST_OUTPUT_GRACE: Duration = Duration::from_secs(3);
const CURSOR_REPLY: &[u8] = b"\x1b[1;1R";

#[derive(Clone, Serialize)]
#[serde(tag = "kind", rename_all = "camelCase")]
pub enum TerminalEvent {
    Output { data: String },
    Exit { code: Option<u32> },
}

/// Para onde vão a saída de uma sessão e as linhas do diagnóstico. `event` devolve `false` quando a tela já não ouve.
pub trait TerminalSink: Send + Sync + 'static {
    fn event(&self, event: TerminalEvent) -> bool;
    fn note(&self, message: &str);
}

/// O canal do comando do Tauri para a tela e o registro de diagnóstico do host.
pub struct ChannelSink {
    channel: Channel<TerminalEvent>,
    app: AppHandle,
}

impl ChannelSink {
    pub fn new(channel: Channel<TerminalEvent>, app: AppHandle) -> Arc<Self> {
        Arc::new(Self { channel, app })
    }
}

impl TerminalSink for ChannelSink {
    fn event(&self, event: TerminalEvent) -> bool {
        self.channel.send(event).is_ok()
    }

    fn note(&self, message: &str) {
        record(&self.app, Source::Host, message);
    }
}

#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
pub struct TerminalOpened {
    pub session_id: String,
    pub shell: String,
}

pub fn clamp_size(cols: u16, rows: u16) -> PtySize {
    PtySize {
        cols: cols.clamp(2, 400),
        rows: rows.clamp(2, 200),
        pixel_width: 0,
        pixel_height: 0,
    }
}

struct Session {
    /// Vira `None` quando o shell sai: fechar o pty é o que destrava a leitura no Windows (ConPTY).
    master: Mutex<Option<Box<dyn MasterPty + Send>>>,
    /// Um shell que não lê a entrada prenderia quem escreve; com a escrita numa thread da sessão, nem a tela nem o
    /// encerramento (que responde ao cursor) esperam por ele.
    writer: Writer,
    child: Mutex<Box<dyn Child + Send + Sync>>,
    pid: Option<u32>,
}

struct PtyInput(Box<dyn Write + Send>);

impl Output for PtyInput {
    fn write(&mut self, data: &[u8]) -> bool {
        self.0.write_all(data).and_then(|()| self.0.flush()).is_ok()
    }
}

#[derive(Clone, Default)]
pub struct Terminals {
    sessions: Arc<Mutex<HashMap<String, Arc<Session>>>>,
    next_id: Arc<AtomicU64>,
}

fn failed(message: &str) -> HostError {
    HostError::new(error_code::TERMINAL_FAILED, message)
}

impl Terminals {
    pub fn open(
        &self,
        project_dir: &Path,
        cols: u16,
        rows: u16,
        sink: Arc<dyn TerminalSink>,
    ) -> Result<TerminalOpened, HostError> {
        if lock(&self.sessions).len() >= MAX_SESSIONS {
            return Err(HostError::new(
                error_code::TERMINAL_LIMIT,
                "Já há 4 terminais abertos. Feche um para abrir outro.",
            ));
        }

        let shell = shell::default_shell();
        let pair = native_pty_system()
            .openpty(clamp_size(cols, rows))
            .map_err(|_| failed("Não foi possível criar o terminal."))?;
        let mut command = CommandBuilder::new(&shell.program);
        command.args(&shell.args);
        command.cwd(project_dir);
        command.env_clear();
        for (name, value) in env::shell_env(std::env::vars()) {
            command.env(name, value);
        }
        let child = pair
            .slave
            .spawn_command(command)
            .map_err(|_| failed("Não foi possível iniciar o shell do terminal."))?;
        drop(pair.slave);
        let reader = pair
            .master
            .try_clone_reader()
            .map_err(|_| failed("Não foi possível ler o terminal."))?;
        let writer = pair
            .master
            .take_writer()
            .map_err(|_| failed("Não foi possível escrever no terminal."))?;

        let session_id = format!("t-{}", self.next_id.fetch_add(1, Ordering::Relaxed) + 1);
        let session = Arc::new(Session {
            pid: child.process_id(),
            master: Mutex::new(Some(pair.master)),
            writer: Writer::spawn(PtyInput(writer)),
            child: Mutex::new(child),
        });
        lock(&self.sessions).insert(session_id.clone(), Arc::clone(&session));
        sink.note(&format!("Terminal {session_id} aberto ({}).", shell.label));

        let reading = read_output(reader, Arc::clone(&sink));
        self.watch_exit(session_id.clone(), session, reading, sink);

        Ok(TerminalOpened {
            session_id,
            shell: shell.label,
        })
    }

    /// Espera o shell sair sem segurar o lock (fechar precisa dele), fecha o pty e espera a última linha antes de avisar
    /// a tela. Nada aqui pode prender a vaga para sempre: o pty fecha numa thread à parte e a espera pela leitura tem
    /// limite.
    fn watch_exit(
        &self,
        session_id: String,
        session: Arc<Session>,
        reading: Receiver<()>,
        sink: Arc<dyn TerminalSink>,
    ) {
        let sessions = Arc::clone(&self.sessions);
        std::thread::spawn(move || {
            let code = loop {
                match lock(&session.child).try_wait() {
                    Ok(Some(status)) => break Some(status.exit_code()),
                    Ok(None) => {}
                    Err(_) => break None,
                }
                std::thread::sleep(EXIT_POLL);
            };
            release_console(&session);
            let master = lock(&session.master).take();
            std::thread::spawn(move || drop(master));
            let _ = reading.recv_timeout(LAST_OUTPUT_GRACE);
            lock(&sessions).remove(&session_id);
            sink.event(TerminalEvent::Exit { code });
            let exit =
                code.map_or_else(|| "sem código".to_string(), |code| format!("código {code}"));
            sink.note(&format!("Terminal {session_id} encerrado ({exit})."));
        });
    }

    fn session(&self, session_id: &str) -> Result<Arc<Session>, HostError> {
        lock(&self.sessions)
            .get(session_id)
            .cloned()
            .ok_or_else(|| {
                HostError::new(
                    error_code::TERMINAL_NOT_FOUND,
                    "Este terminal já foi encerrado.",
                )
            })
    }

    pub fn write(&self, session_id: &str, data: &str) -> Result<(), HostError> {
        if data.len() > MAX_WRITE_BYTES {
            return Err(HostError::new(
                error_code::MESSAGE_TOO_LARGE,
                "O texto colado é grande demais para o terminal de uma vez.",
            ));
        }
        let session = self.session(session_id)?;
        if session.writer.send(data.as_bytes().to_vec()) {
            Ok(())
        } else {
            Err(failed("Não foi possível escrever no terminal."))
        }
    }

    pub fn resize(&self, session_id: &str, cols: u16, rows: u16) -> Result<(), HostError> {
        let session = self.session(session_id)?;
        let master = lock(&session.master);
        let resized = master
            .as_ref()
            .map(|master| master.resize(clamp_size(cols, rows)));
        match resized {
            Some(Ok(())) => Ok(()),
            Some(Err(_)) => Err(failed("Não foi possível ajustar o tamanho do terminal.")),
            None => Err(HostError::new(
                error_code::TERMINAL_NOT_FOUND,
                "Este terminal já foi encerrado.",
            )),
        }
    }

    pub fn close(&self, session_id: &str) -> Result<(), HostError> {
        let session = self.session(session_id)?;
        kill_tree(&session);
        Ok(())
    }

    pub fn close_all(&self) {
        let open: Vec<Arc<Session>> = lock(&self.sessions).values().cloned().collect();
        for session in open {
            kill_tree(&session);
        }
    }
}

/// A leitura bloqueia, então roda numa thread própria até o pty fechar.
/// Avisa pelo canal quando a leitura termina (ou cai), para quem espera ter um limite.
fn read_output(mut reader: Box<dyn Read + Send>, sink: Arc<dyn TerminalSink>) -> Receiver<()> {
    let (finished, done) = mpsc::channel();
    std::thread::spawn(move || {
        let mut decoder = utf8::Utf8Stream::default();
        let mut buffer = [0u8; READ_CHUNK];
        while let Ok(read) = reader.read(&mut buffer) {
            if read == 0 {
                break;
            }
            let data = decoder.push(&buffer[..read]);
            if !data.is_empty() && !sink.event(TerminalEvent::Output { data }) {
                break;
            }
        }
        let _ = finished.send(());
    });
    done
}

/// O ConPTY pergunta a posição do cursor ao iniciar e espera a resposta do terminal da tela. Se a tela fechou antes de
/// responder, o console fica preso nessa espera e não fecha; a resposta o solta. Com o shell já encerrado, ela não
/// chega a programa nenhum.
fn release_console(session: &Session) {
    if cfg!(windows) {
        session.writer.send(CURSOR_REPLY.to_vec());
    }
}

/// O shell pode ter iniciado servidores e observadores: encerrar só ele deixaria esses processos órfãos.
fn kill_tree(session: &Session) {
    if let Some(pid) = session.pid {
        terminate_descendants(pid);
    }
    let _ = lock(&session.child).kill();
}

#[cfg(windows)]
fn terminate_descendants(pid: u32) {
    use std::os::windows::process::CommandExt;
    const CREATE_NO_WINDOW: u32 = 0x0800_0000;
    let _ = std::process::Command::new("taskkill")
        .args(["/PID", &pid.to_string(), "/T", "/F"])
        .creation_flags(CREATE_NO_WINDOW)
        .status();
}

#[cfg(not(windows))]
fn terminate_descendants(pid: u32) {
    // Grupo 0 ou 1 seria o do próprio Spock ou o sistema inteiro.
    if pid <= 1 {
        return;
    }
    // O pty cria uma sessão nova para o shell: o grupo de processos tem o id dele. Sem o "--", o kill do procps lê o
    // "-<pid>" de outro jeito e manda o sinal ao grupo de quem chama, derrubando o próprio host (visto no CI do Linux).
    let _ = std::process::Command::new("kill")
        .args(["-TERM", "--", &format!("-{pid}")])
        .status();
}

#[cfg(test)]
mod tests {
    use super::*;

    #[derive(Default)]
    struct Collected {
        output: Mutex<String>,
        exit: Mutex<Option<Option<u32>>>,
        notes: Mutex<Vec<String>>,
    }

    impl TerminalSink for Collected {
        fn event(&self, event: TerminalEvent) -> bool {
            match event {
                TerminalEvent::Output { data } => lock(&self.output).push_str(&data),
                TerminalEvent::Exit { code } => *lock(&self.exit) = Some(code),
            }
            true
        }

        fn note(&self, message: &str) {
            lock(&self.notes).push(message.to_string());
        }
    }

    fn eventually(mut done: impl FnMut() -> bool) -> bool {
        (0..400).any(|_| {
            if done() {
                return true;
            }
            std::thread::sleep(Duration::from_millis(50));
            false
        })
    }

    // O resultado esperado (spock2, v=|) não aparece no texto digitado: o eco do comando não basta para passar.
    const PROBE: &str = if cfg!(windows) {
        "Write-Output (\"spock\" + 2); Write-Output (\"v=\" + $env:SPOCK_TEST_TOKEN + \"|\")\r"
    } else {
        "echo spock$((1+1)); echo \"v=${SPOCK_TEST_TOKEN}|\"\r"
    };

    #[test]
    fn um_shell_de_verdade_roda_na_pasta_do_projeto_sem_os_segredos_e_encerra() {
        let dir = std::env::temp_dir().join(format!("spock-terminal-{}", std::process::id()));
        std::fs::create_dir_all(&dir).unwrap();
        std::env::set_var("SPOCK_TEST_TOKEN", "valor-que-nao-pode-vazar");
        let terminals = Terminals::default();
        let sink = Arc::new(Collected::default());

        let opened = terminals.open(&dir, 120, 30, sink.clone()).unwrap();
        // O ConPTY pergunta a posição do cursor ao iniciar e espera a resposta; na tela, quem responde é o xterm.js.
        if cfg!(windows) && eventually(|| lock(&sink.output).contains("\x1b[6n")) {
            terminals.write(&opened.session_id, "\x1b[1;1R").unwrap();
        }
        terminals.write(&opened.session_id, PROBE).unwrap();

        assert!(
            eventually(|| lock(&sink.output).contains("spock2")),
            "{}",
            lock(&sink.output)
        );
        assert!(eventually(|| lock(&sink.output).contains("v=|")));
        assert!(!lock(&sink.output).contains("valor-que-nao-pode-vazar"));
        let folder = dir.file_name().unwrap().to_string_lossy().into_owned();
        assert!(lock(&sink.output).contains(&folder));

        terminals.close(&opened.session_id).unwrap();

        assert!(eventually(|| lock(&sink.exit).is_some()));
        assert!(terminals.write(&opened.session_id, "x").is_err());
        let notes = lock(&sink.notes).join(" | ");
        assert!(
            notes.contains("aberto") && notes.contains("encerrado"),
            "{notes}"
        );
        let _ = std::fs::remove_dir_all(&dir);
    }

    #[test]
    fn fechar_logo_ao_abrir_libera_a_vaga_mesmo_sem_a_tela_ter_respondido_ao_cursor() {
        // Acontece ao trocar de projeto enquanto o shell inicia: a tela some antes de responder ao ConPTY.
        let dir = std::env::temp_dir().join(format!("spock-terminal-cedo-{}", std::process::id()));
        std::fs::create_dir_all(&dir).unwrap();
        let terminals = Terminals::default();
        let sink = Arc::new(Collected::default());

        let opened = terminals.open(&dir, 80, 24, sink.clone()).unwrap();
        terminals.close(&opened.session_id).unwrap();

        assert!(
            eventually(|| lock(&sink.exit).is_some()),
            "a sessão não terminou"
        );
        assert!(
            lock(&terminals.sessions).is_empty(),
            "a vaga continuou ocupada"
        );
        assert!(lock(&sink.notes)
            .iter()
            .any(|note| note.contains("encerrado")));
        let _ = std::fs::remove_dir_all(&dir);
    }

    #[test]
    fn o_tamanho_pedido_pela_tela_fica_dentro_dos_limites() {
        let tiny = clamp_size(0, 0);
        let huge = clamp_size(5000, 5000);

        assert_eq!((tiny.cols, tiny.rows), (2, 2));
        assert_eq!((huge.cols, huge.rows), (400, 200));
    }

    #[test]
    fn a_saida_vai_para_a_tela_com_o_tipo_do_evento() {
        let output = serde_json::to_value(TerminalEvent::Output { data: "ok".into() }).unwrap();
        let exit = serde_json::to_value(TerminalEvent::Exit { code: Some(0) }).unwrap();

        assert_eq!(
            output,
            serde_json::json!({ "kind": "output", "data": "ok" })
        );
        assert_eq!(exit, serde_json::json!({ "kind": "exit", "code": 0 }));
    }

    #[test]
    fn escrever_num_terminal_que_nao_existe_diz_que_ele_foi_encerrado() {
        let terminals = Terminals::default();

        let error = terminals.write("t-99", "ls").err().unwrap();

        assert_eq!(error.code, error_code::TERMINAL_NOT_FOUND);
    }

    #[test]
    fn texto_grande_demais_e_recusado_antes_de_chegar_ao_shell() {
        let terminals = Terminals::default();
        let big = "x".repeat(MAX_WRITE_BYTES + 1);

        let error = terminals.write("t-1", &big).err().unwrap();

        assert_eq!(error.code, error_code::MESSAGE_TOO_LARGE);
    }
}
