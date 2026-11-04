//! Estado do motor como a UI o recebe: o ciclo de vida do processo e, com ele pronto, o do runtime de IA.

use serde_json::{json, Value};

#[derive(Clone, Copy, PartialEq, Eq, Debug)]
pub enum Status {
    Starting,
    Ready,
    Crashed,
}

impl Status {
    pub fn as_str(self) -> &'static str {
        match self {
            Status::Starting => "starting",
            Status::Ready => "ready",
            Status::Crashed => "crashed",
        }
    }
}

/// O motor e o runtime de IA são estados diferentes: a inteligência indisponível não derruba o motor.
pub fn compose_status(lifecycle: Status, health: Option<&Value>) -> Value {
    if lifecycle != Status::Ready {
        return json!({ "engine": lifecycle.as_str(), "aiRuntime": "unknown" });
    }
    let ai = health
        .and_then(|value| value.get("aiRuntime"))
        .and_then(Value::as_str)
        .filter(|status| matches!(*status, "ready" | "stopped" | "unavailable"))
        .unwrap_or("unavailable");
    json!({ "engine": "ready", "aiRuntime": ai })
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn inteligencia_indisponivel_nao_derruba_o_motor() {
        let down = compose_status(Status::Ready, Some(&json!({ "aiRuntime": "unavailable" })));
        assert_eq!(down["engine"], "ready");
        assert_eq!(down["aiRuntime"], "unavailable");

        let crashed = compose_status(Status::Crashed, Some(&json!({ "aiRuntime": "ready" })));
        assert_eq!(crashed["engine"], "crashed");
        assert_eq!(crashed["aiRuntime"], "unknown");

        let unknown = compose_status(Status::Ready, Some(&json!({ "aiRuntime": "inventado" })));
        assert_eq!(unknown["aiRuntime"], "unavailable");
    }
}
