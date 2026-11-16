# ADR-0008 — Modelo de distribuição para gestão empresarial

**Status:** proposta em 2026-11-16, aguarda decisão do dono (DD-1 a DD-4).
**Contexto completo:** [../../engineering-intelligence-hub/DEPLOYMENT_MODELS.md](../../engineering-intelligence-hub/DEPLOYMENT_MODELS.md).

## Contexto

A nova direção trata a gestão empresarial como capacidade central e pede que gestores e desenvolvedores recebam
respostas coerentes com acessos diferentes. O SPOCK é local-first, sem servidor, sem login e sem telemetria. As
permissões reais já existem nos sistemas de origem (Jira, GitHub, nuvem) e são herdadas pela credencial de cada pessoa.

## Decisão proposta

1. O Modelo A (desktop local por pessoa) continua sendo a base de todas as entregas P0 e P1. O recorte de acesso é o da
   credencial da pessoa mais o vínculo explícito de projeto.
2. O Modelo C (híbrido, com um repositório Git da organização como fonte opcional, só de definições sem segredo) é o
   caminho para compartilhar política e portfólio, a ser detalhado quando houver um segundo usuário na mesma
   organização.
3. O Modelo B (núcleo hospedado) não entra no roadmap sem decisão explícita do dono e um cliente que o exija.

## Consequências

- Nenhuma entrega P0/P1 introduz login, backend, multi-tenancy ou telemetria.
- A auditoria continua sem "quem" até a decisão DD-2.
- Papéis (gestor, desenvolvedor) não são impostos pelo SPOCK no Modelo A: são consequência das credenciais conectadas.
  O estudo não promete o contrário.

## Alternativas rejeitadas por ora

- Backend hospedado com multi-tenancy: inverte local-first, cria custo de operação e de conformidade.
- Sincronizar o estado do SPOCK entre máquinas por serviço de terceiros: dado sensível fora da máquina sem decisão.
