//! O host só confere a forma do envelope; o conteúdo (`params`, `result`, `data`) é validado pelo motor (C#) e pela
//! UI (TypeScript).

use serde::Serialize;
use serde_json::{json, Value};

pub const PROTOCOL_VERSION: u64 = 1;
pub const MAX_MESSAGE_BYTES: usize = 4 * 1024 * 1024;

/// Mudou um método no motor (`ProtocolMethods.cs`)? Esta lista e `engine-client.ts` mudam juntas;
/// `engine-methods.test.ts` confere a UI contra ela, e `HostProtocolTests.cs` confere esta lista contra o motor.
/// `workspace.authorize`, `runtime.health` e `engine.*` ficam de fora de propósito: o primeiro recebe um caminho e só o
/// diálogo nativo (comando `pick_project_folder`) pode originá-lo; os demais são do host.
pub const FRONTEND_METHODS: &[&str] = &[
    "project.discover",
    "project.architecture",
    "harness.plan",
    "harness.apply",
    "harness.validate",
    "harness.revert",
    "harness.runs",
    "run.snapshot",
    "run.cancel",
    "run.list",
    "run.detail",
    "run.link",
    "run.unlink",
    "run.linked",
    "workflow.start",
    "workflow.resume",
    "model.status",
    "model.save",
    "model.remove",
    "model.default",
    "model.connection",
    "model.test",
    "model.purpose",
    "model.budget",
    "integration.catalog",
    "connection.list",
    "connection.create",
    "connection.test",
    "connection.rotate",
    "connection.disconnect",
    "connection.remove",
    "connection.reach",
    "integration.list",
    "integration.link",
    "integration.configure",
    "integration.unlink",
    "integration.sync",
    "integration.history",
    "management.portfolio",
    "management.metrics",
    "management.forecast",
    "management.risks",
    "management.riskhistory",
    "management.report",
    "management.milestones",
    "management.evidence",
    "management.ask",
    "management.investigate",
    "management.portfolioevidence",
    "management.portfolioask",
    "decision.record",
    "decision.list",
    "decision.get",
    "modernization.plan",
    "modernization.decide",
    "engineering.profile",
    "engineering.analyze",
    "engineering.knowledge",
    "engineering.harness",
    "engineering.revisions",
    "engineering.catalog",
    "timeline.read",
    "audit.read",
    "engineering.mcp",
    "engineering.policy",
    "aitool.list",
    "agent.run",
    "ide.connection",
    "context.review",
    "security.scan",
    "security.plan",
    "security.fix",
    "idea.draft",
    "idea.plan",
    "idea.save",
    "cloud.catalog",
    "cloud.list",
    "cloud.create",
    "cloud.validate",
    "cloud.permissions",
    "cloud.remove",
    "cloud.links",
    "cloud.link",
    "cloud.configure",
    "cloud.unlink",
    "cloud.read",
    "iac.runtime.status",
    "iac.runtime.offer",
    "iac.runtime.install",
    "iac.runtime.decline",
    "iac.runtime.remove",
    "iac.runtime.adopt",
    "iac.runtime.pin",
    "iac.layout",
    "iac.validate",
    "iac.plan",
    "iac.operations",
    "iac.operation",
    "iac.apply",
    "iac.state",
    "iac.state.resources",
    "iac.state.unlock",
    "iac.bootstrap.plan",
    "iac.bootstrap.save",
    "iac.architect.draft",
    "iac.architect.proposal",
    "iac.generate.options",
    "iac.generate.plan",
    "iac.generate.save",
    "iac.drift",
    "iac.cost.estimate",
    "iac.cost.plan",
    "iac.drift.schedule",
    "iac.drift.schedule.set",
];

pub mod error_code {
    //! Os códigos que vêm do motor passam como estão, por isso `HostError` guarda texto.
    pub const DIALOG_FAILED: &str = "DIALOG_FAILED";
    pub const ENGINE_NOT_FOUND: &str = "ENGINE_NOT_FOUND";
    pub const ENGINE_NOT_RUNNING: &str = "ENGINE_NOT_RUNNING";
    pub const ENGINE_START_FAILED: &str = "ENGINE_START_FAILED";
    pub const ENGINE_STOPPED: &str = "ENGINE_STOPPED";
    pub const ENGINE_TIMEOUT: &str = "ENGINE_TIMEOUT";
    pub const ENGINE_WRITE_FAILED: &str = "ENGINE_WRITE_FAILED";
    pub const INVALID_MESSAGE: &str = "INVALID_MESSAGE";
    pub const INVALID_PARAMS: &str = "INVALID_PARAMS";
    pub const MESSAGE_TOO_LARGE: &str = "MESSAGE_TOO_LARGE";
    pub const METHOD_NOT_ALLOWED: &str = "METHOD_NOT_ALLOWED";
    pub const NOTICES_NOT_FOUND: &str = "NOTICES_NOT_FOUND";
    pub const PATH_NOT_ALLOWED: &str = "PATH_NOT_ALLOWED";
    pub const PATH_NOT_FOUND: &str = "PATH_NOT_FOUND";
    pub const RECENT_NOT_FOUND: &str = "RECENT_NOT_FOUND";
    pub const TERMINAL_FAILED: &str = "TERMINAL_FAILED";
    pub const TERMINAL_LIMIT: &str = "TERMINAL_LIMIT";
    pub const TERMINAL_NOT_FOUND: &str = "TERMINAL_NOT_FOUND";
    pub const UNSUPPORTED_PROTOCOL: &str = "UNSUPPORTED_PROTOCOL";
}

pub mod method {
    pub const ENGINE_HELLO: &str = "engine.hello";
    pub const ENGINE_SHUTDOWN: &str = "engine.shutdown";
    pub const AUTHORIZE_WORKSPACE: &str = "workspace.authorize";
    pub const RUNTIME_HEALTH: &str = "runtime.health";
}

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

/// Sem montar o texto: o limite vale para o que o motor receberia.
fn serialized_len(value: &Value) -> usize {
    struct Counter(usize);
    impl std::io::Write for Counter {
        fn write(&mut self, buf: &[u8]) -> std::io::Result<usize> {
            self.0 += buf.len();
            Ok(buf.len())
        }
        fn flush(&mut self) -> std::io::Result<()> {
            Ok(())
        }
    }
    let mut counter = Counter(0);
    let _ = serde_json::to_writer(&mut counter, value);
    counter.0
}

pub fn validate_frontend_request(envelope: &Value) -> Result<(String, Value), HostError> {
    if serialized_len(envelope) > MAX_MESSAGE_BYTES {
        return Err(HostError::new(
            error_code::MESSAGE_TOO_LARGE,
            "Mensagem acima do limite permitido.",
        ));
    }

    let obj = envelope.as_object().ok_or_else(|| {
        HostError::new(
            error_code::INVALID_MESSAGE,
            "A mensagem não é um objeto JSON.",
        )
    })?;

    if obj.get("v").and_then(Value::as_u64) != Some(PROTOCOL_VERSION) {
        return Err(HostError::new(
            error_code::UNSUPPORTED_PROTOCOL,
            "Versão de protocolo não suportada.",
        ));
    }
    if obj.get("kind").and_then(Value::as_str) != Some("request") {
        return Err(HostError::new(
            error_code::INVALID_MESSAGE,
            "A UI só pode enviar requisições.",
        ));
    }
    match obj.get("id").and_then(Value::as_str) {
        Some(id) if !id.is_empty() && id.len() <= 64 => {}
        _ => {
            return Err(HostError::new(
                error_code::INVALID_MESSAGE,
                "Requisição sem identificador válido.",
            ))
        }
    }

    let method = obj
        .get("method")
        .and_then(Value::as_str)
        .ok_or_else(|| HostError::new(error_code::INVALID_MESSAGE, "Requisição sem método."))?;

    if !FRONTEND_METHODS.contains(&method) {
        return Err(HostError::new(
            error_code::METHOD_NOT_ALLOWED,
            "Este método não está disponível para a interface.",
        ));
    }

    let params = obj
        .get("params")
        .cloned()
        .unwrap_or_else(|| Value::Object(Default::default()));
    if !params.is_object() {
        return Err(HostError::new(
            error_code::INVALID_PARAMS,
            "Os parâmetros precisam ser um objeto.",
        ));
    }
    Ok((method.to_string(), params))
}

#[derive(Debug, PartialEq)]
pub enum EngineMessage {
    Response { id: String, value: Value },
    Event(Value),
}

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

/// O envelope que o host manda ao motor, já com o fim de linha que delimita a mensagem.
pub fn encode_request(id: &str, method: &str, params: Option<Value>) -> Vec<u8> {
    let mut envelope =
        json!({ "v": PROTOCOL_VERSION, "kind": "request", "id": id, "method": method });
    if let Some(params) = params {
        envelope["params"] = params;
    }
    let mut line = envelope.to_string().into_bytes();
    line.push(b'\n');
    line
}

/// A resposta do motor vira o `result` ou o erro que ele mandou; o código do motor passa como está.
pub fn decode_response(response: &Value) -> Result<Value, HostError> {
    if let Some(error) = response.get("error") {
        let code = error
            .get("code")
            .and_then(Value::as_str)
            .unwrap_or("UNKNOWN");
        let message = error
            .get("message")
            .and_then(Value::as_str)
            .unwrap_or("Falha no motor.");
        return Err(HostError::new(code, message));
    }
    response.get("result").cloned().ok_or_else(|| {
        HostError::new(
            error_code::INVALID_MESSAGE,
            "Resposta do motor sem resultado.",
        )
    })
}

#[cfg(test)]
mod tests {
    use super::*;
    use serde_json::json;

    #[test]
    fn ui_nao_pode_chamar_workspace_authorize() {
        let req = json!({"v":1,"kind":"request","id":"a","method":"workspace.authorize","params":{"path":"C:\\x"}});
        assert_eq!(
            validate_frontend_request(&req).unwrap_err().code,
            "METHOD_NOT_ALLOWED"
        );
    }

    #[test]
    fn ui_nao_pode_chamar_metodos_do_ciclo_de_vida_do_host() {
        for method in [
            "engine.hello",
            "engine.shutdown",
            "workspace.authorize",
            "metodo.desconhecido",
        ] {
            let req = json!({"v":1,"kind":"request","id":"a","method":method});
            assert_eq!(
                validate_frontend_request(&req).unwrap_err().code,
                "METHOD_NOT_ALLOWED",
                "{method}"
            );
        }
    }

    #[test]
    fn ui_pode_chamar_os_metodos_da_jornada_com_params_objeto() {
        for method in FRONTEND_METHODS {
            let req = json!({"v":1,"kind":"request","id":"a","method":method,"params":{"projectId":"p-1"}});
            let (m, params) = validate_frontend_request(&req).unwrap();
            assert_eq!(m, *method);
            assert_eq!(params["projectId"], "p-1");
        }
    }

    #[test]
    fn params_que_nao_e_objeto_e_rejeitado() {
        let req = json!({"v":1,"kind":"request","id":"a","method":"harness.plan","params":[1]});
        assert_eq!(
            validate_frontend_request(&req).unwrap_err().code,
            "INVALID_PARAMS"
        );
    }

    #[test]
    fn rejeita_envelopes_malformados() {
        let casos = [
            (json!([1, 2]), "INVALID_MESSAGE"),
            (
                json!({"v":2,"kind":"request","id":"a","method":"x.y"}),
                "UNSUPPORTED_PROTOCOL",
            ),
            (
                json!({"v":1,"kind":"response","id":"a","method":"x.y"}),
                "INVALID_MESSAGE",
            ),
            (
                json!({"v":1,"kind":"request","method":"x.y"}),
                "INVALID_MESSAGE",
            ),
            (
                json!({"v":1,"kind":"request","id":"","method":"x.y"}),
                "INVALID_MESSAGE",
            ),
            (json!({"v":1,"kind":"request","id":"a"}), "INVALID_MESSAGE"),
        ];
        for (envelope, code) in casos {
            assert_eq!(
                validate_frontend_request(&envelope).unwrap_err().code,
                code,
                "{envelope}"
            );
        }
    }

    #[test]
    fn rejeita_mensagem_acima_do_limite() {
        let big = "x".repeat(MAX_MESSAGE_BYTES + 1);
        let req = json!({"v":1,"kind":"request","id":"a","method":"x.y","params":{"d":big}});
        assert_eq!(
            validate_frontend_request(&req).unwrap_err().code,
            "MESSAGE_TOO_LARGE"
        );
    }

    #[test]
    fn interpreta_resposta_e_evento_do_motor() {
        let resp = br#"{"v":1,"kind":"response","id":"h-1","result":{}}"#;
        assert!(
            matches!(parse_engine_line(resp), Some(EngineMessage::Response { id, .. }) if id == "h-1")
        );

        let ev = br#"{"v":1,"kind":"event","runId":"r","type":"t","seq":0}"#;
        assert!(matches!(
            parse_engine_line(ev),
            Some(EngineMessage::Event(_))
        ));
    }

    #[test]
    fn descarta_linhas_fora_do_protocolo() {
        assert_eq!(parse_engine_line(b""), None);
        assert_eq!(parse_engine_line(b"texto solto"), None);
        assert_eq!(
            parse_engine_line(br#"{"v":2,"kind":"response","id":"a"}"#),
            None
        );
        assert_eq!(
            parse_engine_line(br#"{"v":1,"kind":"request","id":"a","method":"x.y"}"#),
            None
        );
        assert_eq!(parse_engine_line(br#"{"v":1,"kind":"response"}"#), None);
    }

    #[test]
    fn a_requisicao_ao_motor_e_uma_linha_do_envelope() {
        let line = encode_request("h-1", "run.list", Some(json!({ "projectId": "p" })));
        assert_eq!(line.last(), Some(&b'\n'));
        let envelope: Value = serde_json::from_slice(&line).unwrap();
        assert_eq!(
            envelope,
            json!({ "v": PROTOCOL_VERSION, "kind": "request", "id": "h-1", "method": "run.list", "params": { "projectId": "p" } })
        );

        let without: Value =
            serde_json::from_slice(&encode_request("h-2", method::ENGINE_SHUTDOWN, None)).unwrap();
        assert!(without.get("params").is_none());
    }

    #[test]
    fn a_resposta_vira_resultado_ou_o_erro_do_motor() {
        assert_eq!(
            decode_response(&json!({ "result": { "ok": true } })),
            Ok(json!({ "ok": true }))
        );
        assert_eq!(
            decode_response(&json!({ "error": { "code": "PATH_NOT_FOUND", "message": "Sumiu." } })),
            Err(HostError::new("PATH_NOT_FOUND", "Sumiu."))
        );
        assert_eq!(
            decode_response(&json!({ "error": {} })),
            Err(HostError::new("UNKNOWN", "Falha no motor."))
        );
        assert_eq!(
            decode_response(&json!({})).unwrap_err().code,
            error_code::INVALID_MESSAGE
        );
    }
}
