//! O ciclo de vida do motor como uma máquina de estados só, no lugar de seis campos soltos (RS-13). As decisões ficam
//! em funções puras: quem as aplica é `Engine`.

use std::sync::atomic::{AtomicU64, Ordering};
use std::time::{Duration, Instant};

use crate::protocol::error_code;
use crate::status::Status;

// Um motor que ficou de pé por este tempo e caiu conta como uma queda nova, com direito a outra tentativa automática.
pub const STABLE_UPTIME: Duration = Duration::from_secs(60);
const MAX_QUICK_RESTARTS: u64 = 1;

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum Lifecycle {
    Starting,
    Ready {
        since: Instant,
    },
    /// O processo subiu e não completou o handshake (protocolo errado, prazo, executável ausente): reiniciar daria no
    /// mesmo, então não gasta a tentativa automática.
    HandshakeFailed,
    Crashed,
    ShuttingDown,
    Stopped,
}

impl Lifecycle {
    pub fn status(self) -> Status {
        match self {
            Lifecycle::Starting => Status::Starting,
            Lifecycle::Ready { .. } => Status::Ready,
            _ => Status::Crashed,
        }
    }
}

/// O processo do motor terminou. Na partida quem decide é o handshake, que vê o pedido falhar; depois de uma falha
/// de handshake o processo foi encerrado de propósito. `None`: nada a fazer aqui.
pub fn on_exit(current: Lifecycle, restarts: &AtomicU64) -> Option<(Lifecycle, bool)> {
    match current {
        Lifecycle::ShuttingDown => Some((Lifecycle::Stopped, false)),
        Lifecycle::Ready { since } => Some((
            Lifecycle::Crashed,
            restart_allowed(restarts, Some(since.elapsed())),
        )),
        Lifecycle::Starting
        | Lifecycle::HandshakeFailed
        | Lifecycle::Crashed
        | Lifecycle::Stopped => None,
    }
}

/// Processo que morreu durante a partida é queda e tem uma tentativa; recusa do motor ou prazo estourado não.
pub fn on_handshake_error(code: &str, restarts: &AtomicU64) -> (Lifecycle, bool) {
    if code == error_code::ENGINE_STOPPED || code == error_code::ENGINE_WRITE_FAILED {
        (Lifecycle::Crashed, restart_allowed(restarts, None))
    } else {
        (Lifecycle::HandshakeFailed, false)
    }
}

/// Um motor que ficou estável por `STABLE_UPTIME` zera a conta, para uma queda esporádica horas depois também ser
/// recuperada.
fn restart_allowed(restarts: &AtomicU64, uptime: Option<Duration>) -> bool {
    if uptime.is_some_and(|up| up >= STABLE_UPTIME) {
        restarts.store(0, Ordering::SeqCst);
    }
    restarts.fetch_add(1, Ordering::SeqCst) < MAX_QUICK_RESTARTS
}

#[cfg(test)]
mod tests {
    use super::*;

    fn ready_for(uptime: Duration) -> Lifecycle {
        Lifecycle::Ready {
            since: Instant::now() - uptime,
        }
    }

    #[test]
    fn queda_logo_apos_subir_tem_uma_tentativa_so() {
        let restarts = AtomicU64::new(0);

        assert_eq!(
            on_exit(ready_for(Duration::from_secs(2)), &restarts),
            Some((Lifecycle::Crashed, true))
        );
        assert_eq!(
            on_exit(ready_for(Duration::from_secs(2)), &restarts),
            Some((Lifecycle::Crashed, false))
        );
    }

    #[test]
    fn motor_estavel_por_um_tempo_ganha_nova_tentativa() {
        let restarts = AtomicU64::new(0);

        on_exit(ready_for(Duration::from_secs(2)), &restarts);
        on_exit(ready_for(Duration::from_secs(2)), &restarts);

        assert_eq!(
            on_exit(ready_for(STABLE_UPTIME), &restarts),
            Some((Lifecycle::Crashed, true))
        );
        assert_eq!(
            on_exit(ready_for(Duration::from_secs(1)), &restarts),
            Some((Lifecycle::Crashed, false))
        );
    }

    #[test]
    fn handshake_recusado_nao_gasta_a_tentativa_e_o_fim_do_processo_nao_decide_de_novo() {
        let restarts = AtomicU64::new(0);

        for code in [
            error_code::UNSUPPORTED_PROTOCOL,
            error_code::ENGINE_TIMEOUT,
            error_code::ENGINE_NOT_FOUND,
        ] {
            assert_eq!(
                on_handshake_error(code, &restarts),
                (Lifecycle::HandshakeFailed, false)
            );
        }
        assert_eq!(on_exit(Lifecycle::HandshakeFailed, &restarts), None);
        assert_eq!(Lifecycle::HandshakeFailed.status(), Status::Crashed);
        assert_eq!(
            on_exit(ready_for(Duration::from_secs(2)), &restarts),
            Some((Lifecycle::Crashed, true))
        );
    }

    #[test]
    fn motor_que_cai_na_partida_tenta_de_novo_uma_vez_e_so_o_handshake_decide() {
        let restarts = AtomicU64::new(0);

        assert_eq!(on_exit(Lifecycle::Starting, &restarts), None);
        assert_eq!(
            on_handshake_error(error_code::ENGINE_STOPPED, &restarts),
            (Lifecycle::Crashed, true)
        );
        assert_eq!(
            on_handshake_error(error_code::ENGINE_WRITE_FAILED, &restarts),
            (Lifecycle::Crashed, false)
        );
    }

    #[test]
    fn encerrar_o_aplicativo_nao_conta_como_queda() {
        let restarts = AtomicU64::new(0);

        assert_eq!(
            on_exit(Lifecycle::ShuttingDown, &restarts),
            Some((Lifecycle::Stopped, false))
        );
        assert_eq!(on_exit(Lifecycle::Stopped, &restarts), None);
        assert_eq!(restarts.load(Ordering::SeqCst), 0);
    }
}
