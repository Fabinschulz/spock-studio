//! Validação do envelope do protocolo v1. O host só confere a forma da mensagem; o conteúdo
//! (`params`, `result`, `data`) é validado pelo motor (C#) e pela UI (TypeScript).

use serde::Serialize;
use serde_json::Value;

pub const PROTOCOL_VERSION: u64 = 1;
pub const MAX_MESSAGE_BYTES: usize = 4 * 1024 * 1024;

/// Métodos que a UI pode pedir ao motor. `workspace.authorize` fica de fora de propósito: ele
/// recebe um caminho, e só o diálogo nativo (comando `pick_project_folder`) pode originá-lo.
pub const FRONTEND_METHODS: &[&str] = &[];

#[derive(Debug, Clone, Serialize, PartialEq)]
pub struct HostError {
    pub code: String,
    pub message: String,
}

impl HostError {
    pub fn new(code: &str, message: &str) -> Self {
        Self {
            code: code.to_string(),
            message: message.to_string(),
        }
    }
}

/// Valida um pedido vindo da UI e devolve `(method, params)`.
pub fn validate_frontend_request(envelope: &Value) -> Result<(String, Value), HostError> {
    let serialized_len = envelope.to_string().len();
    if serialized_len > MAX_MESSAGE_BYTES {
        return Err(HostError::new("MESSAGE_TOO_LARGE", "Mensagem acima do limite permitido."));
    }

    let obj = envelope
        .as_object()
        .ok_or_else(|| HostError::new("INVALID_MESSAGE", "A mensagem não é um objeto JSON."))?;

    if obj.get("v").and_then(Value::as_u64) != Some(PROTOCOL_VERSION) {
        return Err(HostError::new("UNSUPPORTED_PROTOCOL", "Versão de protocolo não suportada."));
    }
    if obj.get("kind").and_then(Value::as_str) != Some("request") {
        return Err(HostError::new("INVALID_MESSAGE", "A UI só pode enviar requisições."));
    }
    match obj.get("id").and_then(Value::as_str) {
        Some(id) if !id.is_empty() && id.len() <= 64 => {}
        _ => return Err(HostError::new("INVALID_MESSAGE", "Requisição sem identificador válido.")),
    }

    let method = obj
        .get("method")
        .and_then(Value::as_str)
        .ok_or_else(|| HostError::new("INVALID_MESSAGE", "Requisição sem método."))?;

    if !FRONTEND_METHODS.contains(&method) {
        return Err(HostError::new("METHOD_NOT_ALLOWED", "Este método não está disponível para a interface."));
    }

    let params = obj.get("params").cloned().unwrap_or_else(|| Value::Object(Default::default()));
    if !params.is_object() {
        return Err(HostError::new("INVALID_PARAMS", "Os parâmetros precisam ser um objeto."));
    }
    Ok((method.to_string(), params))
}

#[derive(Debug, PartialEq)]
pub enum EngineMessage {
    Response { id: String, value: Value },
    Event(Value),
}

/// Interpreta uma linha do stdout do motor. Linhas fora do protocolo são descartadas.
pub fn parse_engine_line(line: &[u8]) -> Option<EngineMessage> {
    if line.is_empty() || line.len() > MAX_MESSAGE_BYTES {
        return None;
    }
    let value: Value = serde_json::from_slice(line).ok()?;
    let obj = value.as_object()?;
    if obj.get("v").and_then(Value::as_u64) != Some(PROTOCOL_VERSION) {
        return None;
    }
    match obj.get("kind").and_then(Value::as_str)? {
        "response" => {
            let id = obj.get("id").and_then(Value::as_str)?.to_string();
            Some(EngineMessage::Response { id, value })
        }
        "event" => Some(EngineMessage::Event(value)),
        _ => None,
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use serde_json::json;

    #[test]
    fn ui_nao_pode_chamar_workspace_authorize() {
        let req = json!({"v":1,"kind":"request","id":"a","method":"workspace.authorize","params":{"path":"C:\\x"}});
        assert_eq!(validate_frontend_request(&req).unwrap_err().code, "METHOD_NOT_ALLOWED");
    }

    #[test]
    fn ui_nao_pode_chamar_nenhum_metodo_enquanto_a_lista_estiver_vazia() {
        let req = json!({"v":1,"kind":"request","id":"a","method":"engine.hello"});
        assert_eq!(validate_frontend_request(&req).unwrap_err().code, "METHOD_NOT_ALLOWED");
    }

    #[test]
    fn rejeita_envelopes_malformados() {
        let casos = [
            (json!([1, 2]), "INVALID_MESSAGE"),
            (json!({"v":2,"kind":"request","id":"a","method":"x.y"}), "UNSUPPORTED_PROTOCOL"),
            (json!({"v":1,"kind":"response","id":"a","method":"x.y"}), "INVALID_MESSAGE"),
            (json!({"v":1,"kind":"request","method":"x.y"}), "INVALID_MESSAGE"),
            (json!({"v":1,"kind":"request","id":"","method":"x.y"}), "INVALID_MESSAGE"),
            (json!({"v":1,"kind":"request","id":"a"}), "INVALID_MESSAGE"),
        ];
        for (envelope, code) in casos {
            assert_eq!(validate_frontend_request(&envelope).unwrap_err().code, code, "{envelope}");
        }
    }

    #[test]
    fn rejeita_mensagem_acima_do_limite() {
        let big = "x".repeat(MAX_MESSAGE_BYTES + 1);
        let req = json!({"v":1,"kind":"request","id":"a","method":"x.y","params":{"d":big}});
        assert_eq!(validate_frontend_request(&req).unwrap_err().code, "MESSAGE_TOO_LARGE");
    }

    #[test]
    fn interpreta_resposta_e_evento_do_motor() {
        let resp = br#"{"v":1,"kind":"response","id":"h-1","result":{}}"#;
        assert!(matches!(parse_engine_line(resp), Some(EngineMessage::Response { id, .. }) if id == "h-1"));

        let ev = br#"{"v":1,"kind":"event","runId":"r","type":"t","seq":0}"#;
        assert!(matches!(parse_engine_line(ev), Some(EngineMessage::Event(_))));
    }

    #[test]
    fn descarta_linhas_fora_do_protocolo() {
        assert_eq!(parse_engine_line(b""), None);
        assert_eq!(parse_engine_line(b"texto solto"), None);
        assert_eq!(parse_engine_line(br#"{"v":2,"kind":"response","id":"a"}"#), None);
        assert_eq!(parse_engine_line(br#"{"v":1,"kind":"request","id":"a","method":"x.y"}"#), None);
        assert_eq!(parse_engine_line(br#"{"v":1,"kind":"response"}"#), None);
    }
}
