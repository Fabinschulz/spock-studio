# Segurança e governança da infraestrutura cloud

Regras obrigatórias da gestão de infraestrutura do Spock. Complementa
[CLOUD_CONNECTION_SECURITY.md](CLOUD_CONNECTION_SECURITY.md) (conexões, só leitura) com o que vale quando o Spock passa
a preparar e aplicar mudanças com Terraform.

## 1. Consentimento

| Ato | Autoriza | Não autoriza |
| --- | --- | --- |
| Instalar o OpenTofu ou o Terraform | Baixar e verificar **a ferramenta e a versão mostradas** | Atualizar depois, usar em projeto sem versão fixada |
| Adotar instalação externa (OpenTofu ou Terraform) | Usar **aquele binário** (hash gravado) | Usar o binário depois de alterado |
| Conectar uma conta | Ler o que o vínculo liberar | Criar recurso |
| Gerar `.tf` | Gravar o diff aprovado | Executar |
| Gerar plano | Mostrar o plano | Aplicar |
| Aprovar um plano | Aplicar **aquele arquivo de plano** | Aplicar outro plano, aplicar de novo |

Toda autorização acontece na tela, por clique da pessoa. Nada roda em segundo plano, inclusive tentativa de instalação
depois de uma recusa.

## 2. Regras

- **Menor privilégio**: a conexão de leitura (descoberta) e a identidade que aplica são separadas; o Spock recomenda um
  perfil/papel de leitura para o dia a dia e um papel de deploy só para `apply`, com SSO e credenciais temporárias.
- **Credenciais**: o Spock não guarda credencial de nuvem; usa a cadeia oficial de cada SDK (SSO da AWS, `az login`,
  ADC do Google). O processo do OpenTofu ou do Terraform recebe só nomes (perfil, subscription, projeto).
- **Separação**: um vínculo cloud por projeto; trava de conta no provider; um state por projeto e ambiente.
- **Ambientes**: workspace por ambiente com guarda contra `default`; produção pede confirmação reforçada no apply.
- **Binário confiável**: OpenTofu só das releases oficiais do projeto e Terraform só da HashiCorp, com assinatura
  OpenPGP conferida pela chave de quem publica e checksum; hash refeito antes de cada uso; nunca no PATH global ([TERRAFORM_DEPENDENCY_MANAGEMENT.md](TERRAFORM_DEPENDENCY_MANAGEMENT.md)).
- **Sem shell**: nenhuma operação monta linha de comando a partir de texto; argumentos fixos por operação.
- **Agentes**: só ferramentas com contrato; nenhuma executa `apply`; o MCP não contorna aprovação nem escopo
  `--project`.
- **State**: tratado como segredo ([TERRAFORM_STATE_MANAGEMENT.md](TERRAFORM_STATE_MANAGEMENT.md)).
- **Destruição**: destaque e segunda confirmação para qualquer `delete` ou substituição.
- **Geração**: a IA nunca escreve HCL; ela propõe um desenho em dados, que passa por checagens determinísticas, e
  modelos fixos escrevem os arquivos. Ids da conta (conta, VPC, subnets, certificado, bucket, e-mail) não vão para o
  modelo de linguagem e, na resposta dele, são substituídos pelos que a pessoa informou. Senhas nunca entram no
  Terraform: o segredo nasce sem valor e a senha do banco fica com o próprio RDS.
- **Drift e custo**: a verificação de drift é um plano `-refresh-only` que nunca grava o state; dela saem só nomes de
  atributo, e o arquivo com os valores é apagado no fim. A estimativa usa só a lista pública de preços, pela conexão
  com a capacidade `aws.pricing` liberada no vínculo, e não lê nada da conta.
- **Trilha**: instalação, recusa, adoção, versão do projeto, remoção, planos, drift, aprovações e applies na `audit.ndjson`,
  com ids e contagens, nunca valores.

## 3. Análise de segurança do plano (entrega 2, implementada em `IacPlanReader`)

Checagens determinísticas sobre `show -json`, avisando antes da aprovação:

| Provedor | Checagem |
| --- | --- |
| Todos | Recurso destruído ou substituído; provider sem versão exata; backend sem lock ou sem criptografia |
| AWS | Security group com `0.0.0.0/0` em porta que não seja 80/443; bucket sem bloqueio de acesso público ou sem criptografia; política IAM com `"*"` em ação e recurso; ECR com tag mutável; RDS público; falta de `allowed_account_ids` |
| Azure | NSG aberto para a internet; Storage Account com acesso público a blob; Key Vault sem proteção contra exclusão |
| Google | Firewall aberto para `0.0.0.0/0`; bucket com `allUsers`; papel primitivo (`roles/owner`, `roles/editor`) concedido |

Os achados não bloqueiam sozinhos: aparecem na aprovação, e os graves pedem confirmação extra.

## 4. O que já está implementado (entregas 1 a 6)

- OpenTofu (gratuito, MPL 2.0) como padrão; Terraform liberado por escolha explícita (licença com o jurídico).
- Instalação só com autorização, ligada à proposta (`offerId`), recusa respeitada.
- Assinatura e checksum conferidos; zip só com o binário extraído; nada fora da pasta do Spock.
- Hash conferido antes de usar; binário externo só depois de adotado.
- Métodos `iac.*` só para a interface; MCP com `iac_status` só de leitura.
- Caminhos da máquina abreviados (`~`) antes de chegar à interface.
- Validar e planejar sobre uma cópia da configuração: o projeto nunca é tocado; state local copiado só para o plano e apagado em seguida.
- Políticas antes de rodar: pasta descoberta, workspace com nome válido e só selecionado, conexão vinculada, uma operação por vez.
- Aprovação vinculada ao plano exato (id, hash do arquivo e da configuração, configuração do projeto inalterada, prazo de 24 h, plano mais recente, mesma ferramenta); destruição, achados graves e produção pedem confirmação a mais; o plano vale uma vez e o arquivo é apagado depois.
- Apply só do arquivo aprovado, com verificação por plano seguinte limpo; nenhuma ferramenta MCP aplica.
- State: credenciais do bloco backend nunca lidas; state no git, sem trava, sem criptografia e dividido entre projetos apontados; plano recusado se outro projeto usa o mesmo state; state local devolvido ao projeto só se não mudou desde o plano (senão fica em recuperação); force-unlock só com confirmação.
- Escopo de escrita `Infrastructure`: o Spock grava `.tf`, `.tofu`, `.tfvars.example`, o `README.md` de uma pasta e o `.gitignore` da raiz só por execução aprovada e reversível; state, `.tfvars` e código ficam fora.
- Geração no modelo com padrões seguros: banco privado, criptografado e com proteção contra exclusão; ECR com tag imutável; load balancer só HTTPS (TLS 1.3); tarefas que só aceitam o load balancer; IAM sem curinga de serviço; trava de conta no provider; `MARILIZ_LIVE_IAC_PROVIDERS` prova `fmt` e `validate` com o provider real.
- Drift por `plan -refresh-only` sobre a cópia, sem gravar o state, com só nomes de atributo no resultado; custo estimado com a lista pública de preços da AWS pela capacidade `aws.pricing` do vínculo, sem ler a conta e sem inventar preço.
- Achados de segurança do plano (seção 3) já calculados: destruição, substituição, entrada aberta para a internet, IAM com curinga, ECR com tag mutável, banco público, S3 sem bloqueio público, NSG e firewall abertos, bucket público no Google.
