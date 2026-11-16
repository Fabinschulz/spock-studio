# D-11: validador JSON Schema dos testes (relatório técnico)

Data: 2026-11-16. **A decisão aguarda o proprietário. A dependência não foi removida nem substituída.**

## Fatos

- **Pacotes:**
  - `JsonSchema.Net` 9.5.0, referência direta em `runtime/tests/Mariliz.Application.Tests` e
    `runtime/tests/Mariliz.Runtime.IntegrationTests`.
  - Dependências dele: `Json.More.Net` 3.0.1 e `JsonPointer.Net` 7.0.3.
- **Uso:**
  - `ContractFiles.cs` e `EngineProcessTests.cs`, com 11 chamadas de `JsonSchema.FromText`/`Evaluate`.
  - Validam as fixtures e a saída do motor contra os 35 schemas de `contracts/schemas`, todos no **draft 2020-12**.
  - O utilitário `docs/compliance/tools/validate_sbom.cs` também usa a biblioteca para validar os SBOMs.
- **Não distribuídos:**
  - Não aparecem no grafo do `Mariliz.Runtime` (`runtime/Mariliz.Runtime/obj/project.assets.json`) nem no SBOM dos
    instaladores.
  - Os projetos de teste têm `IsPackable=false`.
- **Licença:** o nuspec declara `<license type="file">OSMFEULA.txt</license>`. O EULA "Open Source Maintenance Fee
  Agreement" do json-everything diz:
  - O código-fonte é MIT.
  - O **binário publicado** é fornecido mediante taxa mensal para quem o usa "as part of revenue-generating activities" com
    faturamento anual ≥ US$10.000. Quem já paga suporte aos mantenedores está isento.
  - A taxa "não é taxa de licença". O não pagamento pode suspender o acesso ao binário, mas não impede obter binários de outras
    fontes nem compilar a partir do fonte.
  - Binários compilados pelo próprio usuário não estão sujeitos ao acordo (§4).

## Obrigações hoje

Enquanto o Spock não gera receita, ou a empresa fatura menos de US$10.000 por ano, não há taxa. Quando houver receita acima do
limite, o uso dos binários do NuGet no desenvolvimento do Spock passa a exigir a taxa, mesmo sem distribuição.

## Comparação das opções

| Critério | 1. Pagar a taxa | 2. Compilar do fonte (MIT) | 3. Trocar de validador |
| --- | --- | --- | --- |
| Custo direto | Mensal, valor definido pelo projeto (não consultado) | Nenhum | Nenhum, se o substituto for livre |
| Esforço inicial | Nenhum | Médio: obter o fonte das três bibliotecas na versão usada, compilar e publicar num feed interno ou como projeto local | Médio a alto: reescrever `ContractFiles.cs` e os pontos de `EngineProcessTests.cs` e revalidar os 35 schemas |
| Manutenção | Nenhuma | Alta: cada atualização exige recompilar; correções de segurança chegam por conta própria | Baixa, depois da troca |
| Compatibilidade | Total | Total | Precisa suportar o **draft 2020-12**. Candidatos a avaliar: Corvus.JsonSchema (Apache-2.0) e NJsonSchema (MIT). O suporte de cada um ao 2020-12, as licenças e a ausência de taxa precisam ser confirmados nas fontes oficiais antes da escolha |
| Obrigação de licença | EULA + MIT | MIT: manter aviso | A do substituto |
| Risco | Baixo, com custo recorrente | Baixo jurídico, médio operacional | Baixo, depois de validado |

## Recomendação (não aplicada)

Até haver receita, nada muda. Antes disso, avaliar a opção 3: confirmar suporte ao 2020-12, licença e ausência de taxa do
substituto e medir o esforço da troca em `ContractFiles.cs`. Se a troca for cara, a opção 1 resolve sem esforço técnico. A opção
2 só compensa se a versão for congelada por muito tempo.
