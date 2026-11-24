//! Correlação das requisições com as respostas do motor. Não conhece o processo: escreve por um `Writer` e recebe as
//! respostas que o leitor do stdout entrega em `resolve`.

use std::collections::HashMap;
use std::sync::atomic::{AtomicU64, Ordering};
use std::sync::Mutex;
use std::time::Duration;

use serde_json::Value;
use tokio::sync::oneshot;

use crate::lock;
use crate::protocol::{decode_response, encode_request, error_code, HostError};
use crate::writer::Writer;

type Pending = Mutex<HashMap<String, oneshot::Sender<Value>>>;

#[derive(Default)]
pub struct Rpc {
    pending: Pending,
    next_id: AtomicU64,
}

impl Rpc {
    /// O prazo vale para a escrita e a resposta juntas: um motor que parou de ler o stdin dá ENGINE_TIMEOUT, não trava.
    pub async fn call(
        &self,
        writer: Option<Writer>,
        method: &str,
        params: Option<Value>,
        timeout: Duration,
    ) -> Result<Value, HostError> {
        let writer = writer.ok_or_else(|| {
            HostError::new(
                error_code::ENGINE_NOT_RUNNING,
                "O motor não está em execução.",
            )
        })?;
        let id = format!("h-{}", self.next_id.fetch_add(1, Ordering::SeqCst) + 1);
        let (tx, rx) = oneshot::channel();
        let _pending = PendingEntry::register(&self.pending, id.clone(), tx);
        let written = writer.send_acked(encode_request(&id, method, params));

        let exchange = async {
            if written.await != Ok(true) {
                return Err(HostError::new(
                    error_code::ENGINE_WRITE_FAILED,
                    "Não foi possível falar com o motor.",
                ));
            }
            rx.await.map_err(|_| {
                HostError::new(
                    error_code::ENGINE_STOPPED,
                    "O motor encerrou antes de responder.",
                )
            })
        };
        let response = tokio::time::timeout(timeout, exchange)
            .await
            .map_err(|_| {
                HostError::new(
                    error_code::ENGINE_TIMEOUT,
                    "O motor demorou demais para responder.",
                )
            })??;
        decode_response(&response)
    }

    pub fn resolve(&self, id: &str, value: Value) {
        if let Some(tx) = lock(&self.pending).remove(id) {
            let _ = tx.send(value);
        }
    }

    /// Descartar os remetentes faz as requisições pendentes falharem com ENGINE_STOPPED.
    pub fn fail_all(&self) {
        lock(&self.pending).clear();
    }

    #[cfg(test)]
    fn waiting(&self) -> usize {
        lock(&self.pending).len()
    }
}

/// Tira a requisição de `pending` em qualquer saída de `call`: erro de escrita, prazo estourado ou o future
/// descartado por quem pediu. Se a resposta chegou antes, o leitor já a removeu e aqui não sobra nada.
struct PendingEntry<'a> {
    pending: &'a Pending,
    id: String,
}

impl<'a> PendingEntry<'a> {
    fn register(pending: &'a Pending, id: String, tx: oneshot::Sender<Value>) -> Self {
        lock(pending).insert(id.clone(), tx);
        Self { pending, id }
    }
}

impl Drop for PendingEntry<'_> {
    fn drop(&mut self) {
        lock(self.pending).remove(&self.id);
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::sync::Arc;
    use std::time::Instant;

    use serde_json::json;

    use crate::writer::tests::FakePipe;

    fn run<T>(future: impl std::future::Future<Output = T>) -> T {
        tauri::async_runtime::block_on(future)
    }

    #[test]
    fn motor_que_parou_de_ler_o_stdin_da_prazo_e_nao_trava_as_outras() {
        let rpc = Rpc::default();
        let (pipe, _release) = FakePipe::stuck();
        let writer = Writer::spawn(pipe);

        let started = Instant::now();
        let first = run(rpc.call(Some(writer.clone()), "a", None, Duration::from_millis(200)));
        let second = run(rpc.call(Some(writer), "b", None, Duration::from_millis(200)));

        assert_eq!(first.unwrap_err().code, error_code::ENGINE_TIMEOUT);
        assert_eq!(second.unwrap_err().code, error_code::ENGINE_TIMEOUT);
        assert!(started.elapsed() < Duration::from_secs(3));
        assert_eq!(rpc.waiting(), 0);
    }

    #[test]
    fn pipe_fechado_falha_na_escrita_e_sem_processo_nem_escreve() {
        let rpc = Rpc::default();
        let broken = Writer::spawn(FakePipe {
            broken: true,
            ..FakePipe::default()
        });

        let failed = run(rpc.call(Some(broken), "a", None, Duration::from_secs(5)));
        let absent = run(rpc.call(None, "a", None, Duration::from_secs(5)));

        assert_eq!(failed.unwrap_err().code, error_code::ENGINE_WRITE_FAILED);
        assert_eq!(absent.unwrap_err().code, error_code::ENGINE_NOT_RUNNING);
        assert_eq!(rpc.waiting(), 0);
    }

    #[test]
    fn a_resposta_do_stdout_chega_a_quem_pediu_e_o_fim_do_motor_solta_os_outros() {
        let rpc = Arc::new(Rpc::default());
        let pipe = FakePipe::default();
        let writer = Writer::spawn(pipe.clone());

        let answering = Arc::clone(&rpc);
        let answered = tauri::async_runtime::spawn(async move {
            answering
                .call(Some(writer), "a", Some(json!({})), Duration::from_secs(5))
                .await
        });
        while rpc.waiting() == 0 {
            std::thread::sleep(Duration::from_millis(10));
        }
        rpc.resolve("h-1", json!({ "id": "h-1", "result": { "ok": true } }));
        assert_eq!(run(answered).unwrap().unwrap(), json!({ "ok": true }));

        let stopping = Arc::clone(&rpc);
        let writer = Writer::spawn(pipe);
        let stopped = tauri::async_runtime::spawn(async move {
            stopping
                .call(Some(writer), "b", None, Duration::from_secs(5))
                .await
        });
        while rpc.waiting() == 0 {
            std::thread::sleep(Duration::from_millis(10));
        }
        rpc.fail_all();
        assert_eq!(
            run(stopped).unwrap().unwrap_err().code,
            error_code::ENGINE_STOPPED
        );
    }
}
