# Situação dos conectores cloud (2026-11-15)

Só entra como validado o que rodou contra o provedor de verdade, pelo motor real (`Mariliz.Runtime.exe`) e, quando
indicado, pela interface real (Tauri + WebView2 dirigido pelo CDP). Testes de unidade e de contrato (41 no motor, 5 na
interface, mais o contrato das fixtures) não contam como validação real. Nenhum recurso foi criado, alterado ou apagado
em conta nenhuma.

## Resumo

| Provedor | Implementado | Autenticação validada | Leituras validadas | MCP validado | Interface validada |
| --- | --- | --- | --- | --- | --- |
| AWS | Sim: 3 mecanismos, 9 leituras | **Sim**, com conta real (SSO do IAM Identity Center) | **Sim**: 8 de 9 permitidas e lidas; ECS negado pela própria AWS | **Sim**: leitura de S3 e EC2 por agente | **Sim**: validar, verificar permissões, ler EC2 |
| Microsoft Azure | Sim: 2 mecanismos, 7 leituras | Só o caminho "não autenticado": não há login do Azure nesta máquina (Azure CLI ausente, nenhuma credencial na cadeia padrão) | Não: sem identidade | Não | Estado "Aguardando autenticação" exibido |
| Google Cloud | Sim: 2 mecanismos, 5 leituras, verificação por `testIamPermissions` | Só o caminho "não autenticado": não há Application Default Credentials nem `gcloud` nesta máquina | Não: sem identidade | Não | Conexão criada pelo formulário e exibida como "Aguardando autenticação" |

Azure e Google estão **implementados, não validados**: o código compila contra os SDKs oficiais, os erros de cada
SDK têm teste de mapeamento, mas nenhuma chamada autenticada foi feita. Não declare estes provedores prontos antes de
repetir a seção 3 com uma identidade de leitura.

## 1. AWS: evidências

Ambiente: perfil de SSO do IAM Identity Center com o conjunto de permissões "Dev" da conta de desenvolvimento,
região `sa-east-1`. Sessão aberta pelo usuário com `aws sso login --use-device-code --no-browser`.

| Verificação | Resultado real |
| --- | --- |
| `cloud.create` com o perfil SSO | `connected`; conta e ARN do papel assumido vindos do STS |
| Perfil inexistente | `notAuthenticated`, conexão gravada com a orientação |
| Perfil do `aws login` (`login_session`) | Identidade (usuário IAM) lida pelo STS com o `AWSSDK.Signin`; antes dava `notAuthenticated` com a explicação. O provider AWS 6.73.0 do OpenTofu planeja com o mesmo perfil (`AwsProfileLiveTests`, `Provider_aws_autentica_pelo_perfil_da_conexao`) |
| Cadeia padrão sem sessão válida | `expired` (SSO vencido do perfil padrão) |
| Região `marte-1`, Role ARN malformado | Recusados antes de gravar (`CLOUD_REGION_INVALID`, `CLOUD_SETTINGS_INVALID`) |
| `cloud.permissions` | Permitidas: regiões, EC2, ECR, RDS, S3, Lambda, CloudFormation, CloudWatch. Negada: ECS (`ecs:ListClusters`) |
| `cloud.read` | Regiões, S3, EC2 (sa-east-1), Lambda, RDS (0 instâncias) e alarmes do CloudWatch, com limite e indicação de "havia mais" |
| Leitura com projeto sem vínculo / capacidade não liberada | `CLOUD_NOT_LINKED` / `CLOUD_CAPABILITY_NOT_ENABLED` |
| Remover conexão com vínculo | `CLOUD_CONNECTION_IN_USE` |
| MCP `--project` com S3 e EC2 liberados para agentes | `tools/list` mostra `cloud_connections`, `aws_ec2_instances` e `aws_s3_buckets` com `openWorldHint: true`; `aws_lambda_functions` não existe; as duas leituras voltaram com conta, região e horário |
| Liberação de agentes retirada com o servidor MCP aberto | A chamada seguinte foi recusada sem reiniciar |
| Auditoria | "Leitura cloud: aws.ec2.instances · região sa-east-1, pedida por agente (cliente MCP); 2 itens lidos (havia mais)" e os registros de vínculo e escopo |
| Interface | Tela Conexões cloud: "Validar" devolveu Conectada; "Ver Instâncias EC2" mostrou 8 instâncias reais com as 7 colunas |

## 2. Integração com o Spock

| Item | Situação |
| --- | --- |
| Protocolo `cloud.*` (11 métodos) | No motor, na allowlist do host Rust e na da interface (o teste de paridade compara as duas) |
| Fixtures de contrato `contracts/fixtures/cloud` | Geradas da saída real do motor e anonimizadas (conta, ARN, e-mail e perfil trocados) |
| Servidor MCP | Ferramentas cloud no mesmo servidor do contexto do projeto, com o mesmo escopo `--project` |
| Agentes internos (LangGraph) | Ainda não consomem cloud; ver o roadmap |
| Tamanho do sidecar | 94,9 MB (antes, cerca de 77 MB): os SDKs somam cerca de 18 MB |

## 3. Como repetir a validação real

1. Com uma identidade **só de leitura**, autentique no terminal (`aws sso login`, `az login`, `gcloud auth
   application-default login`).
2. No Spock, crie a conexão, **Validar** e **Verificar permissões**.
3. Vincule a um projeto de teste, libere uma leitura e os agentes, e chame a ferramenta pelo MCP
   (`Mariliz.Runtime.exe mcp --project <id>`).
4. Confira na **Auditoria** do projeto e atualize esta tabela só com o que rodou.

## 4. Limitações conhecidas

- Leituras da AWS fazem no máximo 10 páginas; a resposta diz "havia mais" quando passa do limite pedido.
- No Azure, os recursos vêm da lista genérica do Resource Manager: nome, grupo, região, SKU e tipo, sem detalhes
  específicos de cada serviço.
- No Google, a listagem de projetos e de APIs habilitadas traz a primeira página (até 500 e 200 itens).
- A lista de ferramentas MCP é montada quando a IDE inicia o servidor; liberar uma leitura nova pede reiniciar o
  servidor. Retirar uma liberação vale na hora.
