# Modelos de distribuição: local-first e gestão empresarial

> Parte do estudo [Engineering Intelligence Hub](README.md) (2026-11-16). **Decisão do dono.** Este documento compara;
> não introduz login, backend hospedado, multi-tenancy nem telemetria. A ADR correspondente fica como proposta
> ([../architecture/adr/0008-modelo-de-distribuicao.md](../architecture/adr/0008-modelo-de-distribuicao.md)).

## 1. O ponto de partida

O SPOCK hoje é local-first por desenho: um usuário, uma máquina, dados em `MARILIZ_DATA_DIR`, segredos no cofre do
sistema operacional, nenhum servidor do SPOCK, nenhuma identidade de pessoa (`MARILIZ_CURRENT_STATE.md` §3.7 e §3.8).
Isso é uma força comercial (privacidade, custo zero de operação, nada sai da máquina sem decisão) e um limite para
gestão empresarial: dois gestores não compartilham decisões, e não há como dar a um desenvolvedor menos acesso que a
outro **dentro do SPOCK**.

Uma observação muda o tamanho do problema: **as permissões reais já existem nos sistemas de origem.** O token Jira de
um desenvolvedor só vê o que o Jira deixa ele ver; o mesmo vale para GitHub, Azure DevOps e nuvem. No modelo local, o
SPOCK herda esse recorte de graça. O que falta é o que não tem origem externa: portfólio compartilhado, decisões
gerenciais comuns, política da organização e índices pesados reaproveitados.

## 2. Os três modelos

### Modelo A: desktop local por pessoa (atual)

Cada pessoa instala o SPOCK e conecta as próprias credenciais. O portfólio é o conjunto de projetos autorizados na
máquina dela.

### Modelo B: núcleo compartilhado da organização

Um serviço da organização guarda política, portfólio, decisões, índices e permissões. O desktop vira interface e
cliente desse serviço. Exige identidade (SSO da empresa), autorização por papel, operação e backup.

### Modelo C: híbrido

O processamento e os segredos continuam locais. Um repositório compartilhado **opcional** distribui o que é
organizacional e não secreto: política da organização, catálogo de conectores aprovados, definição de portfólio,
decisões gerenciais publicadas. Pode começar sem servidor: um repositório Git da organização (`spock-org/`) que o SPOCK
lê como mais uma fonte versionada, com o mesmo plano, diff e aprovação do Harness.

## 3. Comparação

| Critério | A local | B compartilhado | C híbrido (Git da organização) |
| --- | --- | --- | --- |
| Segurança | Alta: nada sai da máquina; credencial pessoal | Média: ponto central de ataque, guarda tokens ou índices de vários projetos | Alta: só política e definições sem segredo saem; credenciais ficam locais |
| Privacidade | Alta | Depende do operador | Alta |
| Custo para o cliente | Zero de operação | Servidor, banco, backup, SSO | Um repositório Git, que a empresa já tem |
| Custo para o produto | Zero | Alto: backend, multi-tenancy, suporte, segurança, conformidade | Baixo: reaproveita plano/diff/aprovação e o leitor de fontes |
| Sincronização | Cada pessoa sincroniza com as fontes | Central | Fontes externas por pessoa; política e portfólio pelo Git |
| Multiusuário | Não | Sim | Parcial: compartilha definições, não estado em tempo real |
| Controle de acesso | O da credencial de cada um | Papéis no SPOCK + credencial de serviço | O da credencial de cada um + quem pode escrever no repositório da organização |
| Escalabilidade | Limitada à máquina (índice de projetos grandes, C-08) | Alta | Igual a A para processamento |
| Disponibilidade | A da máquina | A do serviço | A da máquina; o repositório só é lido |
| Comercialização | Licença por usuário; fácil de vender a times pequenos | Assinatura empresarial; exige SOC 2, contrato de dados | Licença por usuário + "edição organização" sem operação hospedada |
| Contradiz decisões aprovadas? | Não | Sim: local-first, sem servidor, sem login | Não, se o repositório for opcional e só leitura pelo SPOCK |

## 4. Recomendação

1. **Manter o Modelo A como base** em todas as entregas P0 e P1. Ele já entrega o cenário 12 do pedido de forma honesta:
   o desenvolvedor só vê o que a credencial dele vê, e o gestor, que conecta credenciais com acesso ao portfólio, vê o
   portfólio.
2. **Desenhar o Modelo C como próximo passo** (P2/P3) quando houver um segundo usuário real na mesma organização. O
   primeiro artefato é um `spock-org.yaml` versionado com política da organização e definição de portfólio, lido pelo
   motor como fonte com origem e hash.
3. **Não construir o Modelo B agora.** Ele inverte três decisões aprovadas (local-first, sem servidor, sem login) e cria
   um produto de operação. Só faz sentido com cliente empresarial que exija e pague, e com decisão explícita do dono.

## 5. O que depende do dono

| Decisão | Opções | Efeito |
| --- | --- | --- |
| DD-1 Modelo de distribuição alvo | A só; A agora e C depois; B | Define se identidade e papéis entram no roadmap |
| DD-2 Identidade de pessoa | Nenhuma; identidade local declarada (nome na auditoria); SSO | Sem identidade, a auditoria não tem "quem" e não há papéis |
| DD-3 Gestão entre pessoas | Cada um com seu portfólio; portfólio publicado no Git da organização | Decide o primeiro artefato do Modelo C |
| DD-4 Comercialização | Licença individual; edição organização | Afeta EULA (D-07) e precificação |
