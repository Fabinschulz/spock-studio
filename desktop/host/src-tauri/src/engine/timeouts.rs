use std::collections::HashMap;
use std::time::Duration;

use serde_json::Value;

// Descoberta e plano leem o projeto e consultam ferramentas; execuções longas respondem na hora e avisam por eventos. Os
// métodos que demoram mais o motor anuncia no engine.hello (ProtocolDeadlines.cs, RS-10): o host não guarda a tabela.
pub const REQUEST_TIMEOUT: Duration = Duration::from_secs(120);

// O motor responde ao hello em poucos segundos. Com os 120 s comuns, um motor travado na partida deixava a UI 2 min em
// "iniciando" (RS-13).
pub const HANDSHAKE_TIMEOUT: Duration = Duration::from_secs(30);

// Um valor absurdo no hello não pode deixar um pedido esperando para sempre.
const MAX_ANNOUNCED: Duration = Duration::from_secs(6 * 60 * 60);

pub type Deadlines = HashMap<String, Duration>;

/// O que o motor anunciou em `deadlines` (segundos por método). Valor que não é um número positivo fica de fora: o
/// método cai no padrão, como faria com um motor que não anuncia nada.
pub fn announced(hello: &Value) -> Deadlines {
    hello
        .get("deadlines")
        .and_then(Value::as_object)
        .map(|entries| {
            entries
                .iter()
                .filter_map(|(method, seconds)| {
                    let seconds = seconds.as_u64().filter(|value| *value > 0)?;
                    Some((
                        method.clone(),
                        Duration::from_secs(seconds).min(MAX_ANNOUNCED),
                    ))
                })
                .collect()
        })
        .unwrap_or_default()
}

pub fn request_timeout(method: &str, deadlines: &Deadlines) -> Duration {
    deadlines.get(method).copied().unwrap_or(REQUEST_TIMEOUT)
}

#[cfg(test)]
mod tests {
    use super::*;
    use serde_json::json;

    #[test]
    fn o_metodo_anunciado_espera_o_que_o_motor_disse_e_o_resto_o_prazo_comum() {
        let deadlines = announced(&json!({
            "protocol": 1,
            "deadlines": { "iac.apply": 5400, "management.investigate": 180 }
        }));

        assert_eq!(
            request_timeout("iac.apply", &deadlines),
            Duration::from_secs(5400)
        );
        assert_eq!(
            request_timeout("management.investigate", &deadlines),
            Duration::from_secs(180)
        );
        assert_eq!(request_timeout("run.snapshot", &deadlines), REQUEST_TIMEOUT);
    }

    #[test]
    fn motor_sem_anuncio_ou_com_valor_estranho_cai_no_padrao_e_nada_espera_para_sempre() {
        assert!(announced(&json!({ "protocol": 1 })).is_empty());

        let deadlines = announced(&json!({
            "deadlines": { "a": 0, "b": -5, "c": "muito", "d": 999_999_999u64 }
        }));

        assert_eq!(request_timeout("a", &deadlines), REQUEST_TIMEOUT);
        assert_eq!(request_timeout("b", &deadlines), REQUEST_TIMEOUT);
        assert_eq!(request_timeout("c", &deadlines), REQUEST_TIMEOUT);
        assert_eq!(request_timeout("d", &deadlines), MAX_ANNOUNCED);
    }

    #[test]
    fn o_handshake_desiste_bem_antes_do_prazo_comum() {
        assert!(HANDSHAKE_TIMEOUT * 2 <= REQUEST_TIMEOUT);
    }
}
