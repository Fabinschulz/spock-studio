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
- **Trilha**: instalação, recusa, adoção, versão do projeto, remoção, planos, aprovações e applies na `audit.ndjson`,
  com ids e contagens, nunca valores.

## 3. Análise de segurança do plano (entrega 2)

Checagens determinísticas sobre `show -json`, avisando antes da aprovação:

| Provedor | Checagem |
| --- | --- |
| Todos | Recurso destruído ou substituído; provider sem versão exata; backend sem lock ou sem criptografia |
| AWS | Security group com `0.0.0.0/0` em porta que não seja 80/443; bucket sem bloqueio de acesso público ou sem criptografia; política IAM com `"*"` em ação e recurso; ECR com tag mutável; RDS público; falta de `allowed_account_ids` |
| Azure | NSG aberto para a internet; Storage Account com acesso público a blob; Key Vault sem proteção contra exclusão |
| Google | Firewall aberto para `0.0.0.0/0`; bucket com `allUsers`; papel primitivo (`roles/owner`, `roles/editor`) concedido |

Os achados não bloqueiam sozinhos: aparecem na aprovação, e os graves pedem confirmação extra.

## 4. O que já está implementado (entrega 1)

- OpenTofu (gratuito, MPL 2.0) como padrão; Terraform só por escolha explícita.
- Instalação só com autorização, ligada à proposta (`offerId`), recusa respeitada.
- Assinatura e checksum conferidos; zip só com o binário extraído; nada fora da pasta do Spock.
- Hash conferido antes de usar; binário externo só depois de adotado.
- Métodos `iac.*` só para a interface; MCP com `terraform_status` só de leitura.
- Caminhos da máquina abreviados (`~`) antes de chegar à interface.
