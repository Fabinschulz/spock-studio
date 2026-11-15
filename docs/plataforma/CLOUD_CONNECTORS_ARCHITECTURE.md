# Arquitetura dos conectores cloud

Como o Spock se conecta a AWS, Microsoft Azure e Google Cloud, descobre o que a identidade alcança e entrega isso à
pessoa, aos projetos e aos agentes, sempre só com leitura. Escrito antes da implementação e mantido junto com ela.
Segurança em [CLOUD_CONNECTION_SECURITY.md](CLOUD_CONNECTION_SECURITY.md), autenticação em
[CLOUD_AUTHENTICATION_GUIDE.md](CLOUD_AUTHENTICATION_GUIDE.md), situação em [CLOUD_CONNECTORS_STATUS.md](CLOUD_CONNECTORS_STATUS.md).

## 1. O que já existia e o que foi reaproveitado

| Mecanismo existente | Onde | Uso na cloud |
| --- | --- | --- |
| Registro de conectores por provedor (`ConnectorRegistry`, `IIntegrationConnector`) | `Application/Integrations` | Mesmo padrão: `CloudProviderRegistry` com uma implementação por provedor; serviços não mudam quando entra um provedor |
| Conexão separada do vínculo com o projeto (`IntegrationConnection` × `ProjectIntegration`) | `Application/Integrations` | Mesmo princípio: conexão ativa não dá acesso a projeto nenhum; o vínculo diz o que cada projeto pode ler |
| Trilha de auditoria (`IAuditLog`, `audit.ndjson`) e leitura por projeto (`ProjectAudit`) | `Application/Audit` | Cada conexão, vínculo, verificação e leitura vira linha da trilha |
| Projetos autorizados (`WorkspaceAuthorizer`) | `Application/Workspace` | Vínculo e leitura só para projeto aberto no Spock |
| Servidor MCP com escopo por projeto (`McpServer`, `--project`) | `Application/Mcp` | Ferramentas cloud entram no mesmo servidor e obedecem ao mesmo escopo |
| Protocolo com allowlist em três camadas (motor, host Rust, interface) e fixtures de contrato | `Protocol`, `src-tauri/src/protocol.rs`, `shared/api` | Grupo de métodos `cloud.*` |
| Gravação atômica e quarentena de arquivo corrompido (`AtomicFile`) | `Infrastructure/Integrations` | Arquivo `cloud/connections.json` |

O que **não** foi reaproveitado, e por quê:

- `IIntegrationConnector.FetchPageAsync`: as integrações sincronizam itens de trabalho em lote. Cloud é consulta sob
  demanda, com resposta que muda a cada minuto: copiar recursos para o disco criaria um inventário velho e mais dado
  sensível parado. Cloud não sincroniza, só lê na hora.
- Cofre de segredos (`ISecretVault`): nenhum mecanismo suportado precisa que o Spock guarde segredo. As credenciais ficam
  onde o SDK oficial de cada provedor já as procura (perfis e cache de SSO da AWS, login do Azure CLI, Application
  Default Credentials do Google). A conexão guarda só nomes: perfil, região, Role ARN, tenant, subscription, projeto.
- Sistema de plugins: o Spock não tem carregamento dinâmico de plugins, e criar um agora seria abstração sem uso. O
  "plugin" de um provedor é a implementação registrada no `CloudProviderRegistry` com o seu descritor (mecanismos,
  capacidades, permissões), que o catálogo, a interface e o MCP leem. Ver seção 6.

## 2. Camadas e responsabilidades

```
Interface (Conexões cloud)        Agente externo (Claude Desktop/Code, Cursor, Codex)
        │ cloud.* (protocolo)                 │ MCP stdio, --project
        ▼                                     ▼
 CloudRequestHandler               CloudMcpTools (adaptador MCP)
        └──────────────┬─────────────────────┘
                       ▼
   Application/Cloud: CloudHub
     CloudConnectionService   criar, validar, verificar permissões, remover
     CloudProjectService      vincular a projeto, capacidades, acesso de agentes
     CloudReadService         única porta de leitura: confere vínculo, capacidade,
                              acesso de agente; chama o provedor; audita
     CloudProviderRegistry    catálogo; recusa provedor desconhecido
                       │ ICloudProvider (porta)
                       ▼
   Infrastructure.Cloud (projeto próprio, único que referencia SDK de cloud)
     Aws/   AWSSDK (STS, SSO, SSOOIDC, EC2, S3, ECS, ECR, RDS, Lambda, CloudFormation, CloudWatch)
     Azure/ Azure.Identity + Azure.ResourceManager
     Gcp/   Google.Apis.Auth + APIs REST (Resource Manager, Compute, Storage, Service Usage)
```

- **Domínio e aplicação não conhecem SDK.** `Mariliz.Application` tem só tipos e a porta `ICloudProvider`. Os SDKs
  ficam em `Mariliz.Infrastructure.Cloud`, referenciado apenas pelo executável (`Mariliz.Runtime`) e pelos testes.
- **Interface e MCP não duplicam regra.** Os dois chamam `CloudReadService`, que é o único lugar que decide se uma
  leitura pode acontecer e que a registra.
- **Agentes nunca tocam no SDK.** Eles só veem ferramentas MCP geradas a partir das capacidades que o projeto liberou
  para agentes.

## 3. Conceitos e onde vivem

| Conceito pedido | Tipo no código | Observação |
| --- | --- | --- |
| Cloud Provider | `CloudProviderDescriptor` + `ICloudProvider` | Nome, versão, mecanismos de autenticação com seus campos, capacidades com permissões exigidas |
| Cloud Connection | `CloudConnection` | Mecanismo, configurações sem segredo, estado, identidade, permissões verificadas |
| Credential Provider | Dentro de cada `ICloudProvider` (por exemplo `AwsCredentials`) | Usa a cadeia oficial do SDK; nunca recebe nem grava segredo |
| Cloud Resource Discovery | Capacidades de leitura (`CloudCapability`) + `ReadAsync` | Uma capacidade é uma operação de leitura com colunas fixas |
| Cloud Operations | `CloudCapability.Impact` | Só `read` nesta entrega; escrita não existe no código (ver segurança) |
| MCP Adapter | `CloudMcpTools` | Uma ferramenta por capacidade liberada, com esquema próprio |
| Spock Plugin | Descritor registrado no `CloudProviderRegistry` | Ver seção 6 |

### Estados da conexão

`CloudConnectionStatus`: `connected`, `notAuthenticated` (aguardando autenticação: não há credencial onde o SDK
procura), `expired` (havia credencial e ela venceu, por exemplo o SSO), `invalidIdentity` (credencial recusada, conta,
subscription ou projeto inexistente), `permissionDenied` (a identidade existe mas não pode nem se identificar ou ler a
conta), `invalidRegion` e `unavailable` (rede, serviço fora do ar). "Não configurada" é a ausência de conexão do
provedor.

A interface separa três coisas, como pede o produto: **provedor disponível** (está no catálogo), **conexão autenticada**
(estado acima) e **capacidade autorizada** (resultado da verificação de permissões, por capacidade: `allowed`, `denied`,
`error`, `unchecked`).

Uma conexão com configuração válida é gravada mesmo quando a autenticação falha por falta ou vencimento de credencial:
não há segredo a proteger, e a pessoa precisa ver "Credenciais expiradas: rode `aws sso login --profile X`" e validar
de novo. Configuração inválida (região inexistente, Role ARN malformado, GUID de tenant errado) é recusada antes de
gravar.

## 4. Contratos

### Porta do provedor

```csharp
public interface ICloudProvider
{
    CloudProviderDescriptor Descriptor { get; }
    CloudSettings Normalize(CloudSettings settings);                       // CLOUD_SETTINGS_INVALID / CLOUD_REGION_INVALID
    string NormalizeRegion(string region);                                 // CLOUD_REGION_INVALID
    Task<CloudIdentity> IdentifyAsync(CloudSettings settings, CancellationToken ct);
    Task<CloudTable> ReadAsync(CloudSettings settings, CloudQuery query, CancellationToken ct);
}

// Opcional: provedor que tem API oficial de "quais destas permissões eu tenho" (Google: testIamPermissions).
public interface ICloudPermissionTester
{
    Task<IReadOnlyDictionary<string, bool>> TestPermissionsAsync(CloudSettings settings, IReadOnlyList<string> permissions, CancellationToken ct);
}
```

O provedor lança `EngineException` com os códigos de `CloudErrors` (mapeados das exceções do SDK por uma classe de cada
provedor). O serviço converte o código em estado da conexão. Nenhuma mensagem do provedor passa adiante: só o código e
um texto do Spock, para não vazar ARN de terceiros, cabeçalho ou detalhe de política.

### Protocolo `cloud.*`

| Método | Parâmetros | Resposta |
| --- | --- | --- |
| `cloud.catalog` | nenhum | provedores com mecanismos, campos e capacidades |
| `cloud.list` | nenhum | conexões |
| `cloud.create` | `provider`, `name`, `mechanism`, `settings` | conexão (validada na hora) |
| `cloud.validate` | `connectionId` | conexão com estado e identidade novos |
| `cloud.permissions` | `connectionId` | conexão com a verificação por capacidade |
| `cloud.remove` | `connectionId` | `removed` (recusa se houver vínculo) |
| `cloud.links` | `projectId` | vínculos do projeto |
| `cloud.link` | `projectId`, `connectionId`, `capabilities`, `agentAccess` | vínculo |
| `cloud.configure` | `projectId`, `linkId`, `capabilities?`, `agentAccess?` | vínculo |
| `cloud.unlink` | `projectId`, `linkId` | `removed` |
| `cloud.read` | `connectionId`, `capability`, `region?`, `limit?`, `projectId?` | tabela com colunas fixas da capacidade |

`cloud.read` sem `projectId` é a pessoa explorando a própria conexão na interface. Com `projectId`, só lê o que o
vínculo liberou. O MCP sempre passa projeto e exige `agentAccess`.

### Capacidade

`CloudCapability(Id, Title, Description, Service, Permissions, Region, Columns, Impact)`. `Region` é `none`
(global), `required` (uma região por leitura; sem ela, vale a padrão da conexão, como na AWS) ou `filter` (filtro
opcional; sem ele, vêm todas, como no Azure e no Google). Exemplo: `aws.ec2.instances`, serviço EC2, permissão
`ec2:DescribeInstances`, região `required`, colunas id, nome, tipo, estado, zona, IP privado, início. A permissão listada é o que a identidade precisa ter; o que ela **tem** só se sabe verificando.

### Ferramenta MCP

O id vira nome: `aws.ec2.instances` → `aws_ec2_instances`. Esquema: `project` (texto), `connection` (texto, opcional
quando o projeto tem uma conexão daquele provedor), `region` (em capacidade `required` ou `filter`), `limit` (1 a 100).
Anotações: `readOnlyHint: true`, `openWorldHint: true`. A resposta traz a tabela, a conta, a região, a conexão e o
horário da leitura, que servem de evidência. Há ainda `cloud_connections`, que lista o que o projeto liberou para
agentes.

## 5. Fluxos

1. **Criar**: normaliza a configuração (o provedor recusa formato errado), chama `IdentifyAsync` e grava a conexão com
   o estado que sair. Audita `cloud.connection_created`.
2. **Validar**: `IdentifyAsync` de novo; atualiza estado, identidade e erro. Uma recusa não é exceção, é estado.
3. **Verificar permissões**: com `ICloudPermissionTester`, pergunta ao provedor; sem ele, faz a leitura mínima (limite
   1) de cada capacidade na região padrão. Grava o resultado por capacidade.
4. **Vincular a projeto**: escolhe as capacidades e se agentes podem usá-las. Nasce fechado: sem capacidade e sem agentes.
5. **Ler**: confere projeto autorizado, vínculo, capacidade liberada e, para agente, `agentAccess`; chama o provedor com
   limite; confere que as linhas têm as colunas da capacidade; audita `cloud.read` com quem pediu, capacidade, região,
   quantidade de linhas e resultado. Erro de credencial atualiza o estado da conexão.

## 6. Plugin do Spock

Um provedor é registrado em `LocalCloudHub.Create` (`Infrastructure.Cloud`), como os conectores do GitHub e do Jira
em `LocalIntegrationHub`. O registro entrega ao resto do sistema:

| Precisa | Vem de |
| --- | --- |
| Identificação do provedor | `Descriptor.Provider`, `Name`, `Version` |
| Configuração e mecanismos | `Descriptor.Mechanisms` (campos por mecanismo) |
| Gerenciamento de conexões e validação | `CloudConnectionService`, igual para todos |
| Capacidades e permissões | `Descriptor.Capabilities` |
| Associação a projetos e escopos | `CloudProjectService` |
| Disponibilização controlada a agentes | `agentAccess` do vínculo + `CloudMcpTools` |

Carregar provedores de fora do executável (DLL de terceiros) fica no roadmap: exigiria assinatura e isolamento que não
se justificam com três provedores mantidos aqui.

## 7. Como adicionar um provedor

1. Crie `Infrastructure.Cloud/<Provedor>/` com a classe que implementa `ICloudProvider` e um mapeador de erros do SDK
   para `CloudErrors`.
2. Descreva mecanismos (só os oficiais do provedor, sem segredo guardado pelo Spock) e capacidades de leitura com as
   permissões exigidas e colunas fixas.
3. Registre a classe em `LocalCloudHub.Create` e o id em `CloudProviders`.
4. Testes: normalização de configuração e mapeamento de erros do SDK (`CloudErrorMappingTests`, `CloudConnectorTests`).
   O `CloudProviderRegistry` confere as regras comuns (só leitura, permissões e colunas declaradas, prefixo do id) ao
   registrar, e `Os_provedores_registrados_seguem_as_regras_comuns` roda isso contra todo provedor registrado.
5. Interface: nada a mudar; ela lê mecanismos, campos e capacidades do catálogo (`features/cloud`).
6. Atualize [CLOUD_CONNECTORS_STATUS.md](CLOUD_CONNECTORS_STATUS.md) só com o que foi validado de verdade.
