# Segurança das conexões cloud

O que protege as contas de cloud quando o Spock Studio e os agentes de IA as leem. Princípio: **só leitura, só o que foi
liberado, sempre registrado, sem segredo no Spock**.

## 1. Credenciais

| Controle | Como é feito |
| --- | --- |
| Armazenamento | O Spock não guarda credencial cloud. `cloud/connections.json` tem nome, mecanismo, perfil, região, Role ARN, tenant, subscription, projeto e o caminho de uma chave de service account, nunca o conteúdo |
| Credenciais temporárias | Preferidas por padrão: SSO do IAM Identity Center, AssumeRole de 15 minutos, tokens do Entra ID e do Google renovados pelo próprio SDK |
| Renovação e expiração | Feitas pelo SDK oficial. Credencial vencida vira o estado **Credenciais expiradas**; o Spock nunca abre login interativo sozinho |
| Revogação | Revogar no provedor (sair do SSO, `az logout`, `gcloud auth application-default revoke`) vale na hora: a próxima leitura falha e marca a conexão |
| Vazamento | Mensagens de erro do provedor (com ARN, nome de política, cabeçalho) não passam adiante: só um código `CLOUD_*` e um texto do Spock. O servidor MCP passa o texto de resposta pelo `SecretScanner` antes de entregar |

## 2. Escopo e isolamento

- **Conexão não é acesso.** Uma conexão autenticada não libera nada para projeto nenhum.
- **Vínculo por projeto.** Cada projeto escolhe a conexão e as leituras (capacidades) liberadas. Nasce fechado: zero
  leituras e agentes desligados.
- **Agentes à parte.** `agentAccess` do vínculo decide se agentes podem pedir aquelas leituras. Sem ele, o MCP nem
  lista as ferramentas e recusa a chamada.
- **Isolamento entre projetos.** Uma leitura com projeto só usa vínculo daquele projeto. O MCP com `--project` só
  enxerga os projetos do escopo; projeto fora dele responde como inexistente.
- **Isolamento entre conexões.** Cada leitura escolhe uma conexão explícita; com mais de uma do mesmo provedor no
  projeto, o agente precisa dizer qual.
- **Sem comando arbitrário.** Cada ferramenta MCP é uma leitura com esquema fixo (projeto, conexão, região, limite) e
  colunas fixas. Não há ferramenta que aceite chamada de API livre, filtro livre ou texto que vire comando.
- **Instrução de agente não muda permissão.** A decisão usa só ids (projeto, conexão, capacidade) conferidos contra o
  vínculo gravado; nada do texto do pedido entra na decisão.

## 3. Operações

| Categoria | Situação |
| --- | --- |
| Leitura | Única categoria implementada. Toda capacidade declara `impact: read`; o registro de provedores recusa descritor com outra coisa |
| Escrita | Não existe código de escrita, nem desligado. Se um dia existir, exigirá: capacidade própria com `impact: write`, permissão explícita no vínculo, confirmação da pessoa a cada execução (como as ações aprovadas dos workflows) e trilha própria |

Menor privilégio: cada capacidade lista a permissão exata que exige (por exemplo `ec2:DescribeInstances`,
`Microsoft.Compute/virtualMachines/read`, `compute.instances.list`). A tela mostra, depois de **Verificar
permissões**, o que a identidade tem e o que falta, para quem administra a conta conceder só isso. Políticas prontas
de leitura (`ReadOnlyAccess` na AWS, `Reader` no Azure, `roles/viewer` no Google) funcionam, mas são mais largas do
que o necessário.

## 4. Auditoria

Toda operação cai em `audit.ndjson` e aparece em **Auditoria** do projeto:

| Ação | Categoria | O que registra |
| --- | --- | --- |
| `cloud.connection_created`, `cloud.connection_validated`, `cloud.connection_removed` | Conexões | Conexão, provedor, mecanismo, estado |
| `cloud.permissions_checked` | Conexões | Quantas leituras permitidas, negadas e sem resposta |
| `cloud.linked`, `cloud.scope_changed`, `cloud.unlinked` | Acesso a fontes | Projeto, conexão, leituras liberadas, agentes antes e depois |
| `cloud.read` | Leituras de fontes | Projeto (quando houver), conexão, capacidade, região, quantidade de itens, se havia mais, resultado (`ok` ou código do erro), quem pediu (pessoa ou agente) e o nome que o cliente MCP deu de si |

O conteúdo lido (nomes de instância, buckets) não vai para a trilha: só as contagens.

## 5. Limites conhecidos

- O "quem pediu" do MCP é o nome que o cliente declara; não é identidade conferida. O controle real é o escopo
  `--project` e o `agentAccess`.
- O arquivo de chave de service account do Google continua sendo uma credencial de longa duração no disco da pessoa.
  O Spock só lê o caminho, mas recomenda ADC ou workload identity.
- Não há controle por usuário dentro do Spock: o aplicativo é local e de uma pessoa. O controle por usuário é o do
  próprio provedor (quem consegue fazer login).
