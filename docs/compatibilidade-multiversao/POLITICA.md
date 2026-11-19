# Política de versões

## 1. Projeto existente: respeitar o ambiente

1. A versão-alvo de cada pasta é a que os arquivos do projeto declaram (`modules` do perfil). Não é a do catálogo, nem a do Spock, nem a da máquina.
2. O agente usa só API, sintaxe, pacote e opção de build disponíveis nessa versão. Ele mantém o gerenciador de pacotes e os lockfiles.
3. Um projeto multi-target é limitado pelo alvo mais antigo. Um `engines` é um mínimo, não a versão em uso.
4. Uma versão sem confirmação (faixa declarada, sem instalação) obriga a conferir o lockfile antes de usar recurso novo.
5. Versão desconhecida obriga a ler os manifestos antes de qualquer recurso que dependa de versão.
6. Situação "Legado sem suporte" não bloqueia nada. Ela registra que a atualização é uma modernização à parte (seção 3), nunca parte da tarefa.
7. O contexto entregue ao agente nunca sugere a versão mais nova. A linha mais nova aparece só para o usuário, na tela e na CLI.

## 2. Projeto novo: estável, atual e coerente

Ainda não implementado. A política vale para o MV-5 e o MV-6, e o catálogo atual já tem os dados que ela usa.

1. **Canal.** Só linha `active` com canal `lts`, `sts`, `current` ou `stable`. Nunca `preview`, alpha, beta, rc, nightly ou canary como padrão.
2. **LTS ou mais nova.** Quando houver uma STS ou Current mais nova que a LTS, a escolha é mostrada com a data de fim de cada uma. Exemplo de hoje: .NET 11 (STS) e .NET 10 (LTS), as duas até 2028-11-14. Sem preferência do usuário, a recomendação para produção é a LTS ativa mais nova. No empate de data de fim, como agora, ganha a LTS, porque o caminho de atualização para a próxima LTS é o oficial.
3. **Folga de suporte.** Uma linha com fim em menos de 12 meses não é recomendada para projeto novo, mesmo estando ativa.
4. **Combinação, não coleção.** As versões são escolhidas juntas. React define a faixa de TypeScript e de `@types/react`. Vite define o Node mínimo. O SDK do .NET define o C#. Os plugins seguem o framework (CLI, API e plugins do Tauri na mesma linha). A regra de requisito entre tecnologias é o MV-3.
5. **Registro.** O projeto gerado registra cada versão escolhida, o motivo (critério acima), a fonte e a data do catálogo. "Toda vez que um template é usado, o Spock sabe o que escolheu e por quê."

## 3. Modernização: explícita e incremental

Ainda não implementada (MV-7, sobre o `modernization.plan` do W4-2).

1. Só começa quando o usuário pede. Nunca junto com uma correção.
2. O plano parte da compatibilidade (o que está sem suporte, com o motivo) e lista breaking changes por salto, de uma linha para a seguinte, sem pular LTS sem justificativa.
3. Cada passo é reversível, tem critério de aceite (build, testes, smoke) e precisa de aprovação.
4. Mudança de linguagem, runtime ou framework major é sempre um passo próprio.

## 4. Atualização das dependências do próprio Spock

| Tipo | Regra |
|---|---|
| Patch | Pode ser automatizado depois de passar por build, testes, smoke do instalador e `generate_sbom.py --check` |
| Minor | PR automatizado com as mesmas validações, mais a leitura das notas de release |
| Major | PR à mão, com análise de impacto, plano de migração e validação específica. Nunca silencioso nem com merge automático |
| Runtime embarcado (CPython, .NET do motor) | Sempre tratado como major. Exige o smoke do instalador nas três plataformas |
| Pré-lançamento | Nunca, salvo um ramo de avaliação declarado |

Os ecossistemas não seguem o mesmo versionamento semântico. Algumas exceções conhecidas:

- **TypeScript:** um minor pode quebrar tipos. Toda atualização dele é tratada como minor com typecheck obrigatório.
- **Pacotes `System.*` e `Microsoft.Extensions.*`:** seguem o trem do .NET. A linha correta é a do TFM (10.0.x para `net10.0`), não a mais alta.
- **Python:** um minor do interpretador é major para o Spock.
- **Crates em 0.x:** um minor é major.

Merge automático só acontece com tudo verde. Uma atualização que muda o aviso de licença (exemplo: ProtectedData 10.0.15, que publica só a expressão MIT) para até o aviso ser resolvido.

## 5. Governança do catálogo

- O catálogo só muda por PR. O diff é o próprio JSON, e o relatório do gerador vai na descrição.
- Fonte que falha não apaga dado: a entrada anterior é mantida e marcada como `stale`.
- Regressão da linha ativa mais nova é tratada como inconsistência e recusada.
- O motor lê o catálogo embutido. Nenhuma operação de desenvolvimento depende de rede.
- Atualizar o catálogo não executa comando, não instala nada e não muda arquivo de projeto vinculado.
