# Documentação do Spock Studio

O que cada pasta guarda. Documentos de planejamento, auditorias e relatórios de trabalho já encerrado não ficam aqui:
continuam no histórico do git.

| Pasta ou arquivo | Conteúdo | Quem depende |
| --- | --- | --- |
| [ROADMAP.md](ROADMAP.md) | O que ficou pendente em todas as áreas: em andamento, decisões do dono, entregas e validações | — |
| [architecture/](architecture/) | Arquitetura do motor e da interface, e as decisões registradas (ADRs) | — |
| [organizacao/](organizacao/README.md) | Catálogo de referências arquiteturais, anonimizado | O motor gera regras a partir dela |
| [compliance/](compliance/README.md) | Licenças, SBOM, avisos de terceiros e as ferramentas que os geram | CI, host e a tela Sobre |
| [compatibilidade-multiversao/](compatibilidade-multiversao/README.md) | Política, matriz e roadmap de versões | `VersionPolicy` no motor |
| [universal-boot/](universal-boot/README.md) | Camada de orquestração do pedido: catálogo de capacidades, intenção, permissão, Project Boot e recuperação | O Início e o protocolo `boot.*` |
| [modos-de-modelo/](modos-de-modelo/DESENHO.md) | Desenho dos modos Chave, CLI e Sessão | — |
| [ai-engineering-platform/](ai-engineering-platform/README.md) | Estudo e roadmap vigente da plataforma de engenharia com IA | — |
| [engineering-intelligence-hub/](engineering-intelligence-hub/README.md) | Estudo do hub de inteligência, com decisões em aberto | — |
| [plataforma/](plataforma/) | Guias de nuvem e de infraestrutura como código; propostas e roadmap do ciclo em andamento | — |
| [redesign/](redesign/workspace-unificado.md) | Workspace unificado: navegação, Início, entrada de pedidos e atividade | — |
| [distribuicao.md](distribuicao.md) | O que cada sistema exige para distribuir | — |
| [validacao-real.md](validacao-real.md) | O que falta provar com dados reais e como provar | Testes ao vivo |
