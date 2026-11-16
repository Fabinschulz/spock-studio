# Registro de riscos

> Parte da auditoria estratégica do SPOCK (2026-11-15). Navegação e método em [README.md](README.md).

Probabilidade (P) e impacto (I) em Alta, Média, Baixa. Os riscos já registrados em `docs/compliance/LICENSE_RISK_REGISTER.md`
não são repetidos em detalhe; aqui aparecem só quando a auditoria encontrou um fato novo sobre eles.

| ID | Categoria | Risco | Evidência | P | I | Mitigação recomendada |
|---|---|---|---|---|---|---|
| R-01 | Segurança | IaC com conexão AWS do tipo "assumir papel" planeja e aplica com a identidade do perfil de origem, não do papel | `runtime/Mariliz.Application/Iac/IacOperationService.cs:449-452` usa só `profile ?? sourceProfile` (conferido) | Alta, para quem usa AssumeRole | Alta | Passar o papel ao OpenTofu/Terraform (perfil com `role_arn` ou credenciais temporárias do STS) e testar com conta real |
| R-02 | Licença | Instalador gerado pelo CI distribui a instalação gerenciada do Terraform (BUSL) enquanto D-02 está pendente | `IacDistributionPolicy.ForEdition` trata edição vazia como `Unrestricted`; `ci.yml:149` não passa `SpockEdition` (conferido) | Alta, se houver distribuição | Alta | Decisão do dono sobre D-02; até lá, build comercial com `-p:SpockEdition=commercial` e falha de build se a edição não for informada no CI de release |
| R-03 | Produto | Nenhum fluxo com modelo foi validado com chave real; prompts, prazos (60 s para até 6 chamadas de 30 s) e parsers podem falhar no primeiro uso | AI-23, AI-19 | Alta | Alta | Rodada de validação real com orçamento pequeno, registrando evidência |
| R-04 | Produto | Expectativa de "plataforma de engenharia com agentes" contra entrega de "plano governado" | GAP_ANALYSIS §1 | Alta | Alta | Fechar o elo plano, delegação, verificação (P1) ou ajustar o posicionamento |
| R-05 | Funcional | Teto de tokens aceito pela tela acima do teto do Python: execução falha com `LIMIT_TOKENS` sem explicação | `ModelBudgets.cs:16-21` contra `engineering/state.py:17` e `investigation/graph.py:32` (conferido) | Alta, se a pessoa subir o teto | Média | Uma fonte da verdade para o teto, enviada pelo motor ao Python |
| R-06 | Custo | Três caminhos de IA gastam tokens fora do broker e do teto por execução | S-17 | Média | Média | Teto comum no `ModelGateway` e ADR da fronteira de IA |
| R-07 | Marca | Associação ao personagem de ficção pelo ícone e pelos textos, com registro "SPOCK" de terceiro citado no levantamento de marca | `favicon.svg:3-4`, `spock-studio.svg:3-4`, `evolucao-engineering-management.md:465-466`, `TRADEMARK_ASSESSMENT.md:17` | Média | Alta | Decisão do dono com parecer de marca (D-12) antes de material público |
| R-08 | Licença | Sleepycat (Berkeley DB no Python do Linux) pode exigir oferecer fontes do software que usa o DB; o canal de suporte citado no `SOURCES.md` não existe | INFRASTRUCTURE_MANAGEMENT_AUDIT, LIC-05 | Média | Alta | Parecer jurídico; reavaliar a poda do `_dbm`; alinhar o registro da D-05 |
| R-09 | Governança | Um `--enforce` futuro aprovaria Terraform, Berkeley DB e JsonSchema.Net por estarem "registrados" | `generate_sbom.py:1170,1220-1231`; `license-policy.json` | Média | Média | Estado "pendente de decisão" que bloqueia no modo `--enforce` |
| R-10 | Segurança | Variáveis de ambiente com segredos do motor chegam a CLIs de IA e ferramentas | `SystemProcessRunner.cs` (conferido) | Média | Média | Filtro padrão de segredos, como no terminal |
| R-11 | Segurança | Servidor MCP sem `--project` expõe todos os projetos autorizados à IDE | `McpProjectResolver.cs:8-9` | Média | Média | Recusar iniciar sem projeto, ou exigir lista explícita |
| R-12 | Segurança | Terminal PTY no host sem ADR nem modelo de ameaça | `desktop/host/src-tauri/src/terminal/` | Baixa | Alta | ADR com a fronteira de execução e as mitigações existentes (CSP, ação da pessoa) |
| R-13 | Supply chain | CPython embed do Windows baixado sem conferência de hash no build | `scripts/build-runtime.ps1:70-76` (SC-1 aberto desde 06/11) | Baixa | Alta | Fixar o SHA-256 oficial, como já é feito no Linux |
| R-14 | Supply chain | Verificador OpenPGP próprio sem checar expiração e revogação; sem cosign para o OpenTofu | INF-14 | Baixa | Média | Checar validade da chave; avaliar cosign conforme recomendação do OpenTofu |
| R-15 | Segurança | Arquivo de plano da IaC em texto puro por até 24 h; sem limpeza comprovada de planos não aplicados | INF-21 | Média | Média | Limpeza agendada e permissão restrita no diretório |
| R-16 | Segurança | Produção reconhecida só pelo nome exato do workspace | INFRASTRUCTURE_MANAGEMENT_AUDIT | Média | Média | Marcação explícita de ambiente por vínculo |
| R-17 | Qualidade | Recall do RAG baixo e em queda; documento publica número melhor que o medido | RT-43 | Alta | Média | Ampliar o dataset com projetos reais; medir a cada mudança do índice |
| R-18 | Operação | GitHub Actions sem orçamento: nenhum gate de CI roda de fato; `verify.py` não cobre conformidade nem smoke do instalador | ARCHITECTURE_AUDIT §4.3 | Alta | Média | Rodar `verify.py` antes de cada push e acrescentar o grupo de conformidade |
| R-19 | Qualidade | Testes intermitentes sob carga (UI com prazo de 5 s; um teste Python dependente de ordem) | ARCHITECTURE_AUDIT S14 | Média | Baixa | Prazo maior só nas páginas pesadas; isolar estado do grafo |
| R-20 | Distribuição | Instaladores sem assinatura e sem atualização automática; SHA-256 publicado no mesmo canal não autentica origem | LIC-17, RT-90 | Alta, se distribuído | Média | Certificados e updater antes de usuários externos |
| R-21 | Distribuição | Instalador sem EULA e sem avisos de terceiros | LIC-07, LIC-09 | Alta, se distribuído | Alta | D-07 e D-09 antes de qualquer distribuição |
| R-22 | Arquitetura | Lista de métodos do protocolo em três linguagens, só duas conferidas por teste | ARCHITECTURE_AUDIT S4 | Média | Baixa | Teste .NET contra `protocol.rs` |
| R-23 | Produto | Visão "automação empresarial" contra "não é plataforma de automação" leva a roadmap contraditório | S-19 | Média | Média | Decisão do dono e atualização de `SPOCK_PRODUCT_EVOLUTION.md` |
| R-24 | Documentação | Documentos de decisão desatualizados induzem decisões erradas (blueprint, ADR-0001, estado atual) | GAP_ANALYSIS §5 | Alta | Média | Uma passada de atualização marcando o que foi superado |
| R-25 | Privacidade | Nome de referência externa que o dono pediu para não registrar ainda aparece em uma mensagem de commit e em `docs/MARILIZ_REAL_WORLD_VALIDATION.md:222` | HISTORICAL_EVOLUTION H-67 | Baixa | Baixa | Editar o documento; o commit fica no histórico público |
| R-26 | Validação | Cofre do Linux e do macOS e pacote Linux nunca validados em máquina real | RT-54 | Média | Média | Validação em máquina real antes de distribuir para Linux |
