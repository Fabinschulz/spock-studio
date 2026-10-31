# Mariliz Studio — Implementation Roadmap

> **Nota (2026-10-30):** o produto apresenta um único modelo de implementação, o **Mariliz**. Nomes de kits e metodologias citados neste documento são históricos: na interface e nos contratos o mecanismo de especificação é a "estrutura de especificação" (ver [backend-architecture.md](architecture/backend-architecture.md)).

**Fase:** 0. **Data:** 2026-10-25. Rótulos conforme [`product-discovery.md`](product-discovery.md). Esforço relativo: **P** (dias), **M** (uma a duas semanas), **G** (várias semanas). Não há estimativa de calendário: falta histórico de velocidade para isso.

> **Nota (2026-10-30):** o produto apresenta um único modelo de implementação, o **Mariliz**. Nomes de kits e metodologias citados neste documento são históricos: na interface e nos contratos o mecanismo de especificação é a "estrutura de especificação" (ver [backend-architecture.md](architecture/backend-architecture.md)).

---

> **Nota (2026-10-30):** o produto apresenta um único modelo de implementação, o **Mariliz**. Nomes de kits e metodologias citados neste documento são históricos: na interface e nos contratos o mecanismo de especificação é a "estrutura de especificação" (ver [backend-architecture.md](architecture/backend-architecture.md)).

## 1. Ajustes ao roadmap do briefing

> **Nota (2026-10-30):** o produto apresenta um único modelo de implementação, o **Mariliz**. Nomes de kits e metodologias citados neste documento são históricos: na interface e nos contratos o mecanismo de especificação é a "estrutura de especificação" (ver [backend-architecture.md](architecture/backend-architecture.md)).

| Mudança | Motivo |
| --- | --- |
| Fase 1 dividida em **1a (fundação técnica)** e **1b (jornada Configurar projeto)** | A fundação (Rust, sidecar, protocolo, instalador) é risco técnico; a jornada é a validação de valor. Separadas, cada uma tem critério de saída próprio |
| Núcleo genérico e profiles (Fase 2) **dependem da decisão de titularidade** | Sem autorização, os agentes e skills precisam ser escritos do zero, o que muda o esforço de M para G |
| A Fase 1b já usa um núcleo mínimo (verifier, code-reviewer, contrato de evidência) | Sem algum conteúdo, a jornada não tem o que configurar; o mínimo pode ser escrito do zero se a titularidade demorar |
| Tier 0 de evals entra na Fase 1b, não na Fase 2 | É o que permite afirmar que o harness gerado está correto |
| Fases 3 em diante só começam depois do critério de continuidade da seção 5 | Evita construir a plataforma antes de validar o produto |

> **Nota (2026-10-30):** o produto apresenta um único modelo de implementação, o **Mariliz**. Nomes de kits e metodologias citados neste documento são históricos: na interface e nos contratos o mecanismo de especificação é a "estrutura de especificação" (ver [backend-architecture.md](architecture/backend-architecture.md)).

---

> **Nota (2026-10-30):** o produto apresenta um único modelo de implementação, o **Mariliz**. Nomes de kits e metodologias citados neste documento são históricos: na interface e nos contratos o mecanismo de especificação é a "estrutura de especificação" (ver [backend-architecture.md](architecture/backend-architecture.md)).

## 2. Fases priorizadas

> **Nota (2026-10-30):** o produto apresenta um único modelo de implementação, o **Mariliz**. Nomes de kits e metodologias citados neste documento são históricos: na interface e nos contratos o mecanismo de especificação é a "estrutura de especificação" (ver [backend-architecture.md](architecture/backend-architecture.md)).

### Fase 0 — Discovery e arquitetura · concluída (aguardando aprovação)

> **Nota (2026-10-30):** o produto apresenta um único modelo de implementação, o **Mariliz**. Nomes de kits e metodologias citados neste documento são históricos: na interface e nos contratos o mecanismo de especificação é a "estrutura de especificação" (ver [backend-architecture.md](architecture/backend-architecture.md)).

Entregáveis: os quatro documentos desta pasta.

> **Nota (2026-10-30):** o produto apresenta um único modelo de implementação, o **Mariliz**. Nomes de kits e metodologias citados neste documento são históricos: na interface e nos contratos o mecanismo de especificação é a "estrutura de especificação" (ver [backend-architecture.md](architecture/backend-architecture.md)).

### Fase 1a — Fundação técnica · M

> **Nota (2026-10-30):** o produto apresenta um único modelo de implementação, o **Mariliz**. Nomes de kits e metodologias citados neste documento são históricos: na interface e nos contratos o mecanismo de especificação é a "estrutura de especificação" (ver [backend-architecture.md](architecture/backend-architecture.md)).

**Objetivo:** provar o caminho completo UI → Rust → motor .NET → UI, com instalador Windows funcional e builds das outras plataformas compilando no CI.

> **Nota (2026-10-30):** o produto apresenta um único modelo de implementação, o **Mariliz**. Nomes de kits e metodologias citados neste documento são históricos: na interface e nos contratos o mecanismo de especificação é a "estrutura de especificação" (ver [backend-architecture.md](architecture/backend-architecture.md)).

| Entregável | Critério de aceite | Teste |
| --- | --- | --- |
| Ambiente de desenvolvimento: Rust (`rustup`, toolchain MSVC), Visual Studio Build Tools com C++ | `cargo --version` e `pnpm tauri info` sem erros | Manual, registrado no README |
| Monorepo: `pnpm-workspace.yaml`, `Mariliz.sln`, estrutura de pastas da seção 4.4 do briefing (só o necessário) | `pnpm install` e `dotnet build` verdes | CI |
| `contracts` com envelope v1, `engine.hello`, `workspace.authorize`, `project.discover` (stub) | Fixtures validam em TS e C# | Teste de contrato nos dois lados |
| `Mariliz.Runtime` com loop NDJSON, handshake, cancelamento, logs em stderr | Responde `engine.hello`; ignora/rejeita mensagem inválida com erro tipado | xUnit + teste de integração que inicia o executável |
| `Mariliz.Cli` com `mariliz version` e `mariliz discover <pasta>` (stub) usando a mesma Application | Saída `--json` válida pelo schema | xUnit |
| `desktop-host` com os 3 comandos, capabilities mínimas, sidecar iniciado pelo Rust | Frontend sem permissão de shell/fs (verificado no arquivo de capabilities) | Teste de integração Rust + inspeção de capabilities no CI |
| `desktop-ui` com a **janela inicial da seção 5.4**, Synthra 0.5.0, tema, seletor de pasta funcionando | Corresponde ao wireframe; navegação por teclado; contraste verificado | Vitest + Testing Library + jsx-a11y; captura de tela anexada ao PR |
| Instalador Windows x64 | Instala, abre, seleciona pasta, motor responde, desinstala limpo | Manual em máquina limpa (VM ou Windows Sandbox) |
| CI matricial (Windows, macOS arm64, Ubuntu) | `tauri build` gera artefato nos três | GitHub Actions |

> **Nota (2026-10-30):** o produto apresenta um único modelo de implementação, o **Mariliz**. Nomes de kits e metodologias citados neste documento são históricos: na interface e nos contratos o mecanismo de especificação é a "estrutura de especificação" (ver [backend-architecture.md](architecture/backend-architecture.md)).

**Dependências:** decisão 3 (assinatura) não bloqueia: instaladores de avaliação podem sair sem assinatura, marcados como tal.

> **Nota (2026-10-30):** o produto apresenta um único modelo de implementação, o **Mariliz**. Nomes de kits e metodologias citados neste documento são históricos: na interface e nos contratos o mecanismo de especificação é a "estrutura de especificação" (ver [backend-architecture.md](architecture/backend-architecture.md)).

### Fase 1b — Jornada "Configurar projeto" · G · implementada (ver [phase-1b-configure-project.md](phase-1b-configure-project.md))

> **Nota (2026-10-30):** o produto apresenta um único modelo de implementação, o **Mariliz**. Nomes de kits e metodologias citados neste documento são históricos: na interface e nos contratos o mecanismo de especificação é a "estrutura de especificação" (ver [backend-architecture.md](architecture/backend-architecture.md)).

**Objetivo:** configurar o harness de um projeto real de teste pela UI e pela CLI, com preview, diff, conflitos, aplicação idempotente e validação.

> **Nota (2026-10-30):** o produto apresenta um único modelo de implementação, o **Mariliz**. Nomes de kits e metodologias citados neste documento são históricos: na interface e nos contratos o mecanismo de especificação é a "estrutura de especificação" (ver [backend-architecture.md](architecture/backend-architecture.md)).

| Entregável | Critério de aceite |
| --- | --- |
| Discovery determinístico (.NET e Next.js) com evidências e ambiguidades | Classifica corretamente os projetos de fixture; nunca classifica só pelo nome de arquivo |
| Profiles `dotnet` e `nextjs` v0.1 (status `experimental`) | Manifestos passam no Tier 0 |
| Núcleo mínimo: `verifier`, `code-reviewer`, contrato de evidência | Manifestos com versão, permissões e limites |
| Adaptador Claude Code | Gera `.claude/agents`, `.claude/skills`, `settings.json` (permissões + hooks de política) |
| `mariliz.yaml` + `mariliz.lock` + Plan/Apply de três vias, backup e Desfazer | Segunda execução não escreve nada; arquivo personalizado vira conflito |
| Lint Tier 0 no motor (`mariliz validate`) | Detecta referência quebrada, permissão incoerente, segredo em arquivo gerado |
| Telas Contexto → Análise → Configuração → Revisão → Aplicar → Resultado | Estados vêm do motor; cancelar e retomar funcionam |
| CLI `discover`, `plan`, `apply`, `validate` | Mesmos resultados da UI para o mesmo projeto |

> **Nota (2026-10-30):** o produto apresenta um único modelo de implementação, o **Mariliz**. Nomes de kits e metodologias citados neste documento são históricos: na interface e nos contratos o mecanismo de especificação é a "estrutura de especificação" (ver [backend-architecture.md](architecture/backend-architecture.md)).

> **Situação (implementada):** o escopo foi entregue com estas diferenças em relação à tabela acima: o SDD é o **TLC** (único), em vez de profiles `dotnet`/`nextjs`; o agente Harness Engineer é **original** (não havia um existente com autorização comprovada para reuso) e escreve uma proposta validada pelo motor; só `claude-code` gera configuração; os contratos são validados por fixtures golden (C#) e schemas Zod (UI), sem geração de tipos TS. Limitações abertas e mapa de testes em [phase-1b-configure-project.md](phase-1b-configure-project.md).

> **Nota (2026-10-30):** o produto apresenta um único modelo de implementação, o **Mariliz**. Nomes de kits e metodologias citados neste documento são históricos: na interface e nos contratos o mecanismo de especificação é a "estrutura de especificação" (ver [backend-architecture.md](architecture/backend-architecture.md)).

**Teste com projetos distintos:** cópias descartáveis (fora do repositório original) de um projeto .NET e de um Next.js de referência, **somente se a decisão 1 autorizar**; caso contrário, projetos sintéticos criados para o teste. Os harness instalados nos projetos de referência não são alterados.

> **Nota (2026-10-30):** o produto apresenta um único modelo de implementação, o **Mariliz**. Nomes de kits e metodologias citados neste documento são históricos: na interface e nos contratos o mecanismo de especificação é a "estrutura de especificação" (ver [backend-architecture.md](architecture/backend-architecture.md)).

### Fase 2 — Núcleo e profiles consolidados · M a G

> **Nota (2026-10-30):** o produto apresenta um único modelo de implementação, o **Mariliz**. Nomes de kits e metodologias citados neste documento são históricos: na interface e nos contratos o mecanismo de especificação é a "estrutura de especificação" (ver [backend-architecture.md](architecture/backend-architecture.md)).

Consolidar as forks dos agentes genéricos numa versão de referência; profile `.NET` e `nextjs` em status `validated`; skills de convenção; memória `.mariliz/` (specs, decisões, regressões); escolha do método (TLC ou próprio). **Depende das decisões 1 e 2.**

> **Nota (2026-10-30):** o produto apresenta um único modelo de implementação, o **Mariliz**. Nomes de kits e metodologias citados neste documento são históricos: na interface e nos contratos o mecanismo de especificação é a "estrutura de especificação" (ver [backend-architecture.md](architecture/backend-architecture.md)).

### Fase 3 — Requisitos e documentação · G

> **Nota (2026-10-30):** o produto apresenta um único modelo de implementação, o **Mariliz**. Nomes de kits e metodologias citados neste documento são históricos: na interface e nos contratos o mecanismo de especificação é a "estrutura de especificação" (ver [backend-architecture.md](architecture/backend-architecture.md)).

Importação local (formatos com teste), extração rastreável, revisão item a item, especificação, critérios de aceite, backlog e roadmap configuráveis. Avisos de envio a provedor de modelo.

> **Nota (2026-10-30):** o produto apresenta um único modelo de implementação, o **Mariliz**. Nomes de kits e metodologias citados neste documento são históricos: na interface e nos contratos o mecanismo de especificação é a "estrutura de especificação" (ver [backend-architecture.md](architecture/backend-architecture.md)).

### Fase 4 — Integrações · G

> **Nota (2026-10-30):** o produto apresenta um único modelo de implementação, o **Mariliz**. Nomes de kits e metodologias citados neste documento são históricos: na interface e nos contratos o mecanismo de especificação é a "estrutura de especificação" (ver [backend-architecture.md](architecture/backend-architecture.md)).

Cofre de segredos, catálogo curado de MCP, conectores GitHub e Jira (leitura primeiro), bancos somente leitura (SQL Server, PostgreSQL, MySQL), verificação em quatro níveis. Azure DevOps depois de GitHub e Jira.

> **Nota (2026-10-30):** o produto apresenta um único modelo de implementação, o **Mariliz**. Nomes de kits e metodologias citados neste documento são históricos: na interface e nos contratos o mecanismo de especificação é a "estrutura de especificação" (ver [backend-architecture.md](architecture/backend-architecture.md)).

### Fase 5 — Expansão tecnológica · M por profile

> **Nota (2026-10-30):** o produto apresenta um único modelo de implementação, o **Mariliz**. Nomes de kits e metodologias citados neste documento são históricos: na interface e nos contratos o mecanismo de especificação é a "estrutura de especificação" (ver [backend-architecture.md](architecture/backend-architecture.md)).

Angular, Flutter, React Native, cada um com matriz de compatibilidade testada.

> **Nota (2026-10-30):** o produto apresenta um único modelo de implementação, o **Mariliz**. Nomes de kits e metodologias citados neste documento são históricos: na interface e nos contratos o mecanismo de especificação é a "estrutura de especificação" (ver [backend-architecture.md](architecture/backend-architecture.md)).

### Fase 6 — Modernização e migração · G

> **Nota (2026-10-30):** o produto apresenta um único modelo de implementação, o **Mariliz**. Nomes de kits e metodologias citados neste documento são históricos: na interface e nos contratos o mecanismo de especificação é a "estrutura de especificação" (ver [backend-architecture.md](architecture/backend-architecture.md)).

Discovery de legado, RAG local só quando a busca simples não bastar (com eval de recall desde o primeiro dia), mapeamento origem/destino, migração com checkpoints e reconciliação, paridade funcional e visual.

> **Nota (2026-10-30):** o produto apresenta um único modelo de implementação, o **Mariliz**. Nomes de kits e metodologias citados neste documento são históricos: na interface e nos contratos o mecanismo de especificação é a "estrutura de especificação" (ver [backend-architecture.md](architecture/backend-architecture.md)).

### Fase 7 — Feedback e automação · G

> **Nota (2026-10-30):** o produto apresenta um único modelo de implementação, o **Mariliz**. Nomes de kits e metodologias citados neste documento são históricos: na interface e nos contratos o mecanismo de especificação é a "estrutura de especificação" (ver [backend-architecture.md](architecture/backend-architecture.md)).

Registro de falhas, casos de regressão, Tier 3 com baseline, evolução controlada do harness, métricas locais, workflows com estado e aprovação.

> **Nota (2026-10-30):** o produto apresenta um único modelo de implementação, o **Mariliz**. Nomes de kits e metodologias citados neste documento são históricos: na interface e nos contratos o mecanismo de especificação é a "estrutura de especificação" (ver [backend-architecture.md](architecture/backend-architecture.md)).

### Fase 8 — Distribuição e comercial · M

> **Nota (2026-10-30):** o produto apresenta um único modelo de implementação, o **Mariliz**. Nomes de kits e metodologias citados neste documento são históricos: na interface e nos contratos o mecanismo de especificação é a "estrutura de especificação" (ver [backend-architecture.md](architecture/backend-architecture.md)).

Assinatura e notarização, atualização automática, canais estável/prévia, documentação para equipes, licenciamento.

> **Nota (2026-10-30):** o produto apresenta um único modelo de implementação, o **Mariliz**. Nomes de kits e metodologias citados neste documento são históricos: na interface e nos contratos o mecanismo de especificação é a "estrutura de especificação" (ver [backend-architecture.md](architecture/backend-architecture.md)).

---

> **Nota (2026-10-30):** o produto apresenta um único modelo de implementação, o **Mariliz**. Nomes de kits e metodologias citados neste documento são históricos: na interface e nos contratos o mecanismo de especificação é a "estrutura de especificação" (ver [backend-architecture.md](architecture/backend-architecture.md)).

## 3. Dependências técnicas

> **Nota (2026-10-30):** o produto apresenta um único modelo de implementação, o **Mariliz**. Nomes de kits e metodologias citados neste documento são históricos: na interface e nos contratos o mecanismo de especificação é a "estrutura de especificação" (ver [backend-architecture.md](architecture/backend-architecture.md)).

```
1a ──▶ 1b ──▶ 2 ──▶ 3
              │      └──▶ 7
              ├──▶ 4 ──▶ 6
              └──▶ 5
8 pode começar em paralelo a partir de 1b (assinatura, updater)
Decisão 1 (titularidade) ──▶ 2 (e o teste com projetos reais em 1b)
Decisão 2 (método)       ──▶ 2
```

> **Nota (2026-10-30):** o produto apresenta um único modelo de implementação, o **Mariliz**. Nomes de kits e metodologias citados neste documento são históricos: na interface e nos contratos o mecanismo de especificação é a "estrutura de especificação" (ver [backend-architecture.md](architecture/backend-architecture.md)).

---

> **Nota (2026-10-30):** o produto apresenta um único modelo de implementação, o **Mariliz**. Nomes de kits e metodologias citados neste documento são históricos: na interface e nos contratos o mecanismo de especificação é a "estrutura de especificação" (ver [backend-architecture.md](architecture/backend-architecture.md)).

## 4. Critérios de aceite do MVP (fim da Fase 1b) e como serão comprovados

> **Nota (2026-10-30):** o produto apresenta um único modelo de implementação, o **Mariliz**. Nomes de kits e metodologias citados neste documento são históricos: na interface e nos contratos o mecanismo de especificação é a "estrutura de especificação" (ver [backend-architecture.md](architecture/backend-architecture.md)).

| # | Critério (seção 22 do briefing) | Comprovação |
| --- | --- | --- |
| 1 | Usuário inicia a configuração sem conhecer a CLI | Roteiro manual com um usuário que não participou do desenvolvimento |
| 2 | Discovery sem perguntas desnecessárias | Teste: para cada fixture, número de perguntas = número de ambiguidades declaradas |
| 3 | Motor configura o harness de um projeto de teste | Teste de integração end-to-end na fixture |
| 4 | Usuário revisa e aprova | Teste de componente da tela de Revisão + E2E manual |
| 5 | Arquivos e convenções existentes preservados | Teste de três vias com arquivo personalizado |
| 6 | Configuração idempotente | Teste: `apply` duas vezes → zero escritas na segunda |
| 7 | Estados da UI correspondem ao motor | Teste: eventos simulados do motor → estado exibido |
| 8–9 | Processamento local de documentos / sem envio externo não autorizado | **Fora do MVP** (Fase 3); a UI não oferece a capacidade |
| 10 | Conectores distinguem configuração, autenticação, conectividade e operações | **Fora do MVP** (Fase 4) |
| 11 | Agentes e skills versionados e com limites | Tier 0 sobre os manifestos |
| 12 | Memória específica por projeto | Teste: duas fixtures, nenhuma leitura cruzada |
| 13 | Falha conhecida gera caso de regressão | Parcial: Tier 1 do produto; para o harness do cliente, Fase 7 |
| 14 | Testes reproduzíveis | CI verde em duas execuções seguidas sem cache |
| 15 | Compatibilidade declarada com evidência | `compatibility.tested` só lista versões com fixture |
| 16 | Outro desenvolvedor instala e usa | README seguido por pessoa externa |
| 17 | Status planejado/experimental/implementado/validado/suportado | Campo `status` obrigatório nos manifestos e exibido na UI |
| 18 | Sem nuvem para o essencial | Teste com rede desligada: jornada completa funciona |
| 19 | Janela inicial = wireframe, com Synthra real | Captura de tela + revisão; imports verificados contra `index.d.ts` |
| 20 | Frontend sem regra de negócio | Revisão de código + regra de lint proibindo acesso a `@tauri-apps/plugin-fs`/`shell` |
| 21 | Host com permissões mínimas | Verificação automática do arquivo de capabilities no CI |
| 22 | Pacote instalável na plataforma inicial | Instalação em máquina limpa Windows |
| 23 | Builds Windows, macOS, Linux documentados e validados no CI | Artefatos do CI matricial |

> **Nota (2026-10-30):** o produto apresenta um único modelo de implementação, o **Mariliz**. Nomes de kits e metodologias citados neste documento são históricos: na interface e nos contratos o mecanismo de especificação é a "estrutura de especificação" (ver [backend-architecture.md](architecture/backend-architecture.md)).

---

> **Nota (2026-10-30):** o produto apresenta um único modelo de implementação, o **Mariliz**. Nomes de kits e metodologias citados neste documento são históricos: na interface e nos contratos o mecanismo de especificação é a "estrutura de especificação" (ver [backend-architecture.md](architecture/backend-architecture.md)).

## 5. Critério de continuidade depois da Fase 1b

> **Nota (2026-10-30):** o produto apresenta um único modelo de implementação, o **Mariliz**. Nomes de kits e metodologias citados neste documento são históricos: na interface e nos contratos o mecanismo de especificação é a "estrutura de especificação" (ver [backend-architecture.md](architecture/backend-architecture.md)).

| Resultado | Decisão |
| --- | --- |
| Critérios 1–7, 11–23 atendidos **e** pelo menos um usuário ou equipe fora do desenvolvimento usou o produto num projeto próprio e quer continuar | Avançar para a Fase 2 |
| Critérios técnicos atendidos, mas sem interesse externo | Rever a proposta de valor antes de investir nas Fases 3+ |
| Onboarding de um projeto novo exige mudar o motor ou o núcleo | Rever os contratos de profile antes de seguir |

> **Nota (2026-10-30):** o produto apresenta um único modelo de implementação, o **Mariliz**. Nomes de kits e metodologias citados neste documento são históricos: na interface e nos contratos o mecanismo de especificação é a "estrutura de especificação" (ver [backend-architecture.md](architecture/backend-architecture.md)).

---

> **Nota (2026-10-30):** o produto apresenta um único modelo de implementação, o **Mariliz**. Nomes de kits e metodologias citados neste documento são históricos: na interface e nos contratos o mecanismo de especificação é a "estrutura de especificação" (ver [backend-architecture.md](architecture/backend-architecture.md)).

## 6. Estratégia de testes

> **Nota (2026-10-30):** o produto apresenta um único modelo de implementação, o **Mariliz**. Nomes de kits e metodologias citados neste documento são históricos: na interface e nos contratos o mecanismo de especificação é a "estrutura de especificação" (ver [backend-architecture.md](architecture/backend-architecture.md)).

| Camada | Ferramenta | Escopo |
| --- | --- | --- |
| Core / Application | xUnit (+ FluentAssertions, seguindo os projetos .NET existentes) | Regras, Plan/Apply, discovery com fixtures |
| Infrastructure | xUnit com sistema de arquivos temporário | Escrita atômica, backup, path traversal, links simbólicos |
| Engine | Teste de integração que inicia o executável e conversa por stdio | Protocolo, handshake, cancelamento, queda |
| Contratos | Fixtures validadas em TS e C# | Divergência entre linguagens |
| UI | Vitest + Testing Library + `eslint-plugin-jsx-a11y` | Componentes, estados, teclado |
| Host | Testes Rust da ponte; verificação de capabilities | Permissões |
| Empacotamento | CI matricial + roteiro manual em máquina limpa | Instalador |

> **Nota (2026-10-30):** o produto apresenta um único modelo de implementação, o **Mariliz**. Nomes de kits e metodologias citados neste documento são históricos: na interface e nos contratos o mecanismo de especificação é a "estrutura de especificação" (ver [backend-architecture.md](architecture/backend-architecture.md)).

---

> **Nota (2026-10-30):** o produto apresenta um único modelo de implementação, o **Mariliz**. Nomes de kits e metodologias citados neste documento são históricos: na interface e nos contratos o mecanismo de especificação é a "estrutura de especificação" (ver [backend-architecture.md](architecture/backend-architecture.md)).

## 7. Riscos por fase

> **Nota (2026-10-30):** o produto apresenta um único modelo de implementação, o **Mariliz**. Nomes de kits e metodologias citados neste documento são históricos: na interface e nos contratos o mecanismo de especificação é a "estrutura de especificação" (ver [backend-architecture.md](architecture/backend-architecture.md)).

| Fase | Risco principal | Mitigação |
| --- | --- | --- |
| 1a | Curva de Rust e do empacotamento do sidecar | Rust mínimo; spike de 1 método antes do restante |
| 1a | Synthra 0.x quebrar API | Versão exata e wrappers |
| 1b | Plan/Apply de três vias com casos de borda (renomeações, fim de linha CRLF/LF) | Normalização de fim de linha antes do hash (o lint H-07 do Integrador já trata CRLF) e testes por caso |
| 2 | Titularidade negada | Núcleo escrito do zero a partir dos conceitos |
| 3–4 | Envio de conteúdo a terceiros sem consentimento claro | Aviso e autorização por execução, imposto pelo motor |
| 6 | RAG complexo antes da necessidade | Começar por busca lexical + estrutura; vetorial só com eval comparativo |

> **Nota (2026-10-30):** o produto apresenta um único modelo de implementação, o **Mariliz**. Nomes de kits e metodologias citados neste documento são históricos: na interface e nos contratos o mecanismo de especificação é a "estrutura de especificação" (ver [backend-architecture.md](architecture/backend-architecture.md)).

---

> **Nota (2026-10-30):** o produto apresenta um único modelo de implementação, o **Mariliz**. Nomes de kits e metodologias citados neste documento são históricos: na interface e nos contratos o mecanismo de especificação é a "estrutura de especificação" (ver [backend-architecture.md](architecture/backend-architecture.md)).

## 8. Excluído do MVP

> **Nota (2026-10-30):** o produto apresenta um único modelo de implementação, o **Mariliz**. Nomes de kits e metodologias citados neste documento são históricos: na interface e nos contratos o mecanismo de especificação é a "estrutura de especificação" (ver [backend-architecture.md](architecture/backend-architecture.md)).

Importação de documentos, integrações (Jira, Azure DevOps, GitHub, MCP, bancos), migração, modernização, RAG, Angular/Flutter/React Native, evals com LLM, métricas, workflows automáticos e gatilhos por evento, atualização automática, assinatura obrigatória, telemetria, nuvem, contas, licenciamento.

> **Nota (2026-10-30):** o produto apresenta um único modelo de implementação, o **Mariliz**. Nomes de kits e metodologias citados neste documento são históricos: na interface e nos contratos o mecanismo de especificação é a "estrutura de especificação" (ver [backend-architecture.md](architecture/backend-architecture.md)).

---

> **Nota (2026-10-30):** o produto apresenta um único modelo de implementação, o **Mariliz**. Nomes de kits e metodologias citados neste documento são históricos: na interface e nos contratos o mecanismo de especificação é a "estrutura de especificação" (ver [backend-architecture.md](architecture/backend-architecture.md)).

## 9. Decisões necessárias antes de implementar

> **Nota (2026-10-30):** o produto apresenta um único modelo de implementação, o **Mariliz**. Nomes de kits e metodologias citados neste documento são históricos: na interface e nos contratos o mecanismo de especificação é a "estrutura de especificação" (ver [backend-architecture.md](architecture/backend-architecture.md)).

| # | Decisão | Bloqueia | Recomendação |
| --- | --- | --- | --- |
| 1 | Titularidade dos ativos Easymart e EmpregaNet | Fase 2; teste com projetos reais na 1b | Resolver por escrito antes da Fase 2 |
| 2 | Método do núcleo (TLC vs próprio) | Fase 2 | Método próprio enxuto, com atribuição ao TLC onde conceitos forem aproveitados; evita redistribuir um projeto de terceiros |
| 3 | Assinatura de código (Authenticode, Apple Developer) | Distribuição externa | Adiar para antes do primeiro usuário externo |
| 4 | Licença do repositório | Publicação | Proprietária até a decisão comercial |
| 5 | Ícones faltantes na Synthra | Acabamento da 1a | Contribuir `FolderIcon`, `FolderCodeIcon`, `DocumentIcon`, `PlugIcon` na Synthra (mesmo autor), com fallback provisório |
| 6 | Tema padrão | Acabamento da 1a | Seguir o sistema, com paleta da marca do Mariliz Web |
| 7 | macOS Intel | CI | Só se houver usuário-alvo com Mac Intel |
| 8 | Ferramentas de geração/validação de schema | Início da 1a | Decidir no spike de contrato |
| 9 | Versionar o repositório agora (commit inicial destes documentos) | — | Commit após a aprovação destes documentos |

> **Nota (2026-10-30):** o produto apresenta um único modelo de implementação, o **Mariliz**. Nomes de kits e metodologias citados neste documento são históricos: na interface e nos contratos o mecanismo de especificação é a "estrutura de especificação" (ver [backend-architecture.md](architecture/backend-architecture.md)).

---

> **Nota (2026-10-30):** o produto apresenta um único modelo de implementação, o **Mariliz**. Nomes de kits e metodologias citados neste documento são históricos: na interface e nos contratos o mecanismo de especificação é a "estrutura de especificação" (ver [backend-architecture.md](architecture/backend-architecture.md)).

## 10. Plano para começar

> **Nota (2026-10-30):** o produto apresenta um único modelo de implementação, o **Mariliz**. Nomes de kits e metodologias citados neste documento são históricos: na interface e nos contratos o mecanismo de especificação é a "estrutura de especificação" (ver [backend-architecture.md](architecture/backend-architecture.md)).

1. Aprovar estes documentos e responder às decisões 1, 2 e 5.
2. Instalar Rust e o Visual Studio Build Tools com C++ (ação do usuário na máquina).
3. Spike de contrato: um método (`engine.hello`) atravessando TS → Rust → C# → Rust → TS, com fixtures e teste nos dois lados. Critério: o teste falha quando o schema muda.
4. Janela inicial com Synthra e seletor de pasta.
5. Instalador Windows e CI matricial.
6. Só então a jornada Configurar projeto (Fase 1b).
