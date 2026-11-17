# Guia de autenticação dos conectores cloud

Como conectar AWS, Microsoft Azure e Google Cloud no Spock Studio. Em todos os casos o Spock **não pede nem guarda
segredo**: ele usa a cadeia oficial de credenciais do SDK de cada provedor e grava só nomes (perfil, região, tenant,
subscription, projeto). Para conectar, abra **Plataforma › Conexões cloud**, escolha o provedor e o mecanismo, e
clique em **Conectar e validar**. Arquitetura em [CLOUD_CONNECTORS_ARCHITECTURE.md](CLOUD_CONNECTORS_ARCHITECTURE.md).

Se a credencial ainda não existe ou venceu, a conexão é gravada como **Aguardando autenticação** ou **Credenciais
expiradas**, com o comando para resolver. Faça o login no terminal e clique em **Validar**. O Spock nunca abre login
interativo por conta própria: um agente pedindo uma leitura não faz surgir uma janela de login.

## AWS

| Mecanismo | Campos | Onde está a credencial | Como autenticar |
| --- | --- | --- | --- |
| Perfil do AWS CLI | perfil, região padrão | `~/.aws/config` e `~/.aws/credentials` | Perfil de SSO: `aws sso login --profile <perfil>` (sem navegador: `--use-device-code --no-browser`). Perfil do `aws login` (credenciais do console, tipo `login_session`): `aws login --profile <perfil>`. Perfil de chaves: `aws configure --profile <perfil>`. Processo externo e papel assumido do perfil também funcionam |
| Cadeia padrão do SDK | região padrão | Variáveis de ambiente, `AWS_PROFILE`, perfil `default`, SSO, web identity, papel da máquina (EC2/ECS) | O que a máquina já tiver; para SSO, `aws sso login` no perfil padrão |
| Assumir um papel (AssumeRole) | Role ARN, perfil de origem (opcional), região padrão | Credenciais temporárias do STS (15 minutos nas leituras; uma hora num plano, apply, drift ou leitura de state da IaC, que recebe as credenciais do papel e não o perfil de origem), a partir do perfil de origem ou da cadeia padrão | A identidade de origem precisa de `sts:AssumeRole` no papel, e a política de confiança do papel precisa aceitá-la |

- A identidade vem do `sts:GetCallerIdentity`, que não exige permissão.
- A região precisa ser uma região que o SDK conhece; uma inventada é recusada antes de gravar.
- Perfis do `aws login` (tipo `login_session`) são lidos pelo SDK com o pacote `AWSSDK.Signin`; o Spock nunca abre o
  login: sessão vencida vira "credencial expirada" com o comando `aws login --profile <perfil>` para renovar. O provider
  AWS do OpenTofu/Terraform (6.73.0, o do modelo) lê o mesmo perfil pelo `AWS_PROFILE` que o Spock repassa.
- Chave de acesso digitada na tela não existe de propósito: chave de longa duração fica no `~/.aws/credentials`, onde o
  CLI e o SDK já a procuram.

## Microsoft Azure

| Mecanismo | Campos | Onde está a credencial | Como autenticar |
| --- | --- | --- | --- |
| Login do Azure CLI | tenant (opcional), subscription (opcional), região de filtro (opcional) | Cache do `az` (`AzureCliCredential`) | Instale o Azure CLI e rode `az login` (sem navegador: `az login --use-device-code`) |
| Cadeia padrão do Azure.Identity | os mesmos | `DefaultAzureCredential`: variáveis de ambiente (service principal com `AZURE_CLIENT_ID`, `AZURE_TENANT_ID` e `AZURE_CLIENT_SECRET` ou certificado), workload identity, identidade gerenciada, Visual Studio, Azure CLI, Azure PowerShell e Azure Developer CLI | Qualquer um desses; login por navegador fica desligado |

- Tenant e quem é vêm do token do Microsoft Entra ID; a subscription, do Azure Resource Manager. Sem subscription, vale
  a padrão da identidade.
- Service principal: a credencial fica no ambiente do processo, nunca no Spock.

## Google Cloud

| Mecanismo | Campos | Onde está a credencial | Como autenticar |
| --- | --- | --- | --- |
| Application Default Credentials | projeto, região de filtro (opcional) | `GOOGLE_APPLICATION_CREDENTIALS`, o arquivo de `gcloud auth application-default login`, workload identity, metadados da máquina | `gcloud auth application-default login` |
| Arquivo de chave de service account | caminho absoluto do `.json`, projeto, região de filtro (opcional) | O próprio arquivo, onde ele já está | Gerar a chave no console (prefira ADC ou workload identity: chave de longa duração é o mecanismo menos seguro) |

- Quem é vem do e-mail da service account ou do `tokeninfo` do Google (o token vai no corpo da chamada, não na URL).
- O projeto é conferido no Resource Manager; projeto inexistente ou fora do alcance vira "identidade inválida".
- Permissões: o Google tem `projects.testIamPermissions`, então **Verificar permissões** pergunta oficialmente quais
  permissões a identidade tem, sem fazer leitura de teste.

## Estados e o que fazer

| Estado | Significa | O que fazer |
| --- | --- | --- |
| Conectada | O provedor confirmou quem é | Verificar permissões e vincular a projetos |
| Aguardando autenticação | Não há credencial onde o SDK procura | Rodar o login indicado e **Validar** |
| Credenciais expiradas | Havia credencial e ela venceu (SSO, refresh token) | Renovar o login e **Validar** |
| Identidade inválida | Credencial recusada, ou conta, subscription ou projeto inexistente | Conferir a configuração ou a credencial |
| Permissão insuficiente | A identidade existe mas não consegue ler a conta | Pedir a permissão de leitura a quem administra a conta |
| Região inválida | Região que o provedor não conhece | Corrigir a região |
| Indisponível | Rede ou serviço fora do ar | Tentar de novo |
