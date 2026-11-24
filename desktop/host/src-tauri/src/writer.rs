//! Uma thread dona de um pipe que pode bloquear (o stdin do motor, a entrada de um PTY). Quem escreve entrega os bytes
//! por canal e segue: nenhum lock fica preso enquanto o outro lado não lê. Antes, um motor ou shell que parava de ler
//! travava as requisições, o encerramento e a vaga do terminal (RS-12, RS-14).

use std::sync::mpsc::{self, Receiver, Sender};

use tokio::sync::oneshot;

pub trait Output: Send + 'static {
    /// `false` quando o pipe fechou: a thread para.
    fn write(&mut self, data: &[u8]) -> bool;

    /// Chamado só por `Writer::close`. O motor encerra o processo aqui; o terminal não precisa.
    fn finish(self)
    where
        Self: Sized,
    {
    }
}

enum Job {
    Data(Vec<u8>, Option<oneshot::Sender<bool>>),
    Close(Sender<()>),
}

#[derive(Clone)]
pub struct Writer {
    jobs: Sender<Job>,
}

impl Writer {
    pub fn spawn<O: Output>(output: O) -> Self {
        let (jobs, queue) = mpsc::channel::<Job>();
        std::thread::spawn(move || {
            let mut output = Some(output);
            for job in queue {
                match job {
                    Job::Data(data, written) => {
                        let ok = output.as_mut().is_some_and(|output| output.write(&data));
                        if let Some(written) = written {
                            let _ = written.send(ok);
                        }
                        if !ok {
                            break;
                        }
                    }
                    Job::Close(closed) => {
                        if let Some(output) = output.take() {
                            output.finish();
                        }
                        let _ = closed.send(());
                        break;
                    }
                }
            }
        });
        Self { jobs }
    }

    /// Não espera o pipe. `false` quando a thread já parou (pipe fechado ou `close`).
    pub fn send(&self, data: Vec<u8>) -> bool {
        self.jobs.send(Job::Data(data, None)).is_ok()
    }

    /// O recibo diz se os bytes chegaram ao pipe. Ele cai sem valor se a thread parou antes de chegar a eles.
    pub fn send_acked(&self, data: Vec<u8>) -> oneshot::Receiver<bool> {
        let (written, receipt) = oneshot::channel();
        let _ = self.jobs.send(Job::Data(data, Some(written)));
        receipt
    }

    /// Entra na fila atrás do que já foi pedido. Com o pipe travado, o aviso não chega: quem chama decide quanto esperar.
    pub fn close(&self) -> Receiver<()> {
        let (closed, notice) = mpsc::channel();
        let _ = self.jobs.send(Job::Close(closed));
        notice
    }
}

#[cfg(test)]
pub mod tests {
    use super::*;
    use std::sync::{Arc, Mutex};
    use std::time::{Duration, Instant};

    use crate::lock;

    /// Um pipe de mentira: guarda o que chegou, pode travar até ser solto e pode recusar a escrita.
    #[derive(Clone, Default)]
    pub struct FakePipe {
        pub written: Arc<Mutex<Vec<Vec<u8>>>>,
        pub finished: Arc<Mutex<bool>>,
        pub broken: bool,
        pub stuck: Option<Arc<Mutex<Option<mpsc::Receiver<()>>>>>,
    }

    impl FakePipe {
        /// O pipe trava na primeira escrita até o `Sender` devolvido mandar algo ou sair.
        pub fn stuck() -> (Self, mpsc::Sender<()>) {
            let (release, wait) = mpsc::channel();
            let pipe = Self {
                stuck: Some(Arc::new(Mutex::new(Some(wait)))),
                ..Self::default()
            };
            (pipe, release)
        }
    }

    impl Output for FakePipe {
        fn write(&mut self, data: &[u8]) -> bool {
            if let Some(wait) = self.stuck.as_ref().and_then(|stuck| lock(stuck).take()) {
                let _ = wait.recv();
            }
            lock(&self.written).push(data.to_vec());
            !self.broken
        }

        fn finish(self) {
            *lock(&self.finished) = true;
        }
    }

    #[test]
    fn pipe_travado_nao_prende_quem_escreve() {
        let (pipe, release) = FakePipe::stuck();
        let writer = Writer::spawn(pipe.clone());

        let started = Instant::now();
        for line in 0..50 {
            assert!(writer.send(format!("linha {line}").into_bytes()));
        }
        let closed = writer.close();

        assert!(started.elapsed() < Duration::from_millis(200));
        assert!(closed.recv_timeout(Duration::from_millis(200)).is_err());
        drop(release);
        assert!(closed.recv_timeout(Duration::from_secs(5)).is_ok());
        assert_eq!(lock(&pipe.written).len(), 50);
        assert!(*lock(&pipe.finished));
    }

    #[test]
    fn pipe_fechado_para_a_thread_e_o_recibo_diz_que_nao_escreveu() {
        let pipe = FakePipe {
            broken: true,
            ..FakePipe::default()
        };
        let writer = Writer::spawn(pipe.clone());

        let receipt = writer.send_acked(b"primeira".to_vec());
        assert!(!tauri::async_runtime::block_on(receipt).unwrap());

        let after = writer.send_acked(b"depois".to_vec());
        assert!(tauri::async_runtime::block_on(after).is_err());
        assert!(!*lock(&pipe.finished));
    }
}
