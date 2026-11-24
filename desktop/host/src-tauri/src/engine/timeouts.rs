use std::time::Duration;

// Descoberta e plano leem o projeto e consultam ferramentas; execuções longas respondem na hora e avisam por eventos.
pub const REQUEST_TIMEOUT: Duration = Duration::from_secs(120);
// A investigação de gestão conversa com o modelo por até 150 s no motor (o runtime de IA recebe 120 s). O host espera mais que o
// motor: se cortasse antes, a UI mostraria ENGINE_TIMEOUT enquanto o motor ainda termina e grava a resposta.
const INVESTIGATION_TIMEOUT: Duration = Duration::from_secs(180);
const INVESTIGATION_METHOD: &str = "management.investigate";
// O desenho da infraestrutura pode pedir duas respostas ao modelo.
const IAC_ARCHITECT_METHOD: &str = "iac.architect.draft";
// O download do OpenTofu ou do Terraform tem até 10 min no motor (OfficialReleaseToolchain); o host espera um pouco mais.
const TERRAFORM_INSTALL_TIMEOUT: Duration = Duration::from_secs(660);
const TERRAFORM_INSTALL_METHOD: &str = "iac.runtime.install";
// O motor corta validar, planejar, drift, state e unlock em 40 min no total (IacTimeouts.cs); IacTimeoutsTests confere
// estas esperas contra esse total mais a folga do serviço.
const IAC_OPERATION_TIMEOUT: Duration = Duration::from_secs(2700);
const IAC_VALIDATE_METHOD: &str = "iac.validate";
const IAC_PLAN_METHOD: &str = "iac.plan";
// O drift é um plan -refresh-only: mesmo init e mesmo teto do plano.
const IAC_DRIFT_METHOD: &str = "iac.drift";
// Consultar o state e liberar a trava também rodam init (providers e backend).
const IAC_STATE_METHOD: &str = "iac.state.resources";
const IAC_UNLOCK_METHOD: &str = "iac.state.unlock";
// O apply pode criar banco e cluster: até 55 min no motor, e a operação inteira com a verificação fica em 85 min.
const IAC_APPLY_TIMEOUT: Duration = Duration::from_secs(5400);
const IAC_APPLY_METHOD: &str = "iac.apply";

// O motor responde ao hello em poucos segundos. Com os 120 s comuns, um motor travado na partida deixava a UI 2 min em
// "iniciando" (RS-13).
pub const HANDSHAKE_TIMEOUT: Duration = Duration::from_secs(30);

pub fn request_timeout(method: &str) -> Duration {
    match method {
        INVESTIGATION_METHOD | IAC_ARCHITECT_METHOD => INVESTIGATION_TIMEOUT,
        TERRAFORM_INSTALL_METHOD => TERRAFORM_INSTALL_TIMEOUT,
        IAC_VALIDATE_METHOD | IAC_PLAN_METHOD | IAC_DRIFT_METHOD | IAC_STATE_METHOD
        | IAC_UNLOCK_METHOD => IAC_OPERATION_TIMEOUT,
        IAC_APPLY_METHOD => IAC_APPLY_TIMEOUT,
        _ => REQUEST_TIMEOUT,
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn a_investigacao_espera_mais_que_o_prazo_do_motor_e_o_resto_o_prazo_comum() {
        // O motor dá 150 s à investigação (InvestigationService.Deadline): o host nunca pode desistir antes dele.
        assert!(request_timeout("management.investigate") > Duration::from_secs(150));
        assert_eq!(request_timeout("management.ask"), REQUEST_TIMEOUT);
        assert_eq!(request_timeout("run.snapshot"), REQUEST_TIMEOUT);
        assert!(request_timeout("iac.runtime.install") > Duration::from_secs(600));
        assert!(request_timeout("iac.plan") > Duration::from_secs(2400));
        assert!(request_timeout("iac.drift") > Duration::from_secs(2400));
        assert!(request_timeout("iac.apply") > Duration::from_secs(5000));
    }

    #[test]
    fn o_handshake_desiste_bem_antes_do_prazo_comum() {
        assert!(HANDSHAKE_TIMEOUT * 2 <= REQUEST_TIMEOUT);
    }
}
