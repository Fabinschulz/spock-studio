# Conformidade de licenciamento

Auditoria de licenças, dependências e SBOM do Spock Studio feita em 2026-11-15, para distribuição como software proprietário.
É uma auditoria técnica, não um parecer jurídico.

| Documento | Para que serve |
| --- | --- |
| [LICENSE_AUDIT.md](LICENSE_AUDIT.md) | Resultado geral: resumo executivo, critérios, arquitetura, Terraform/OpenTofu, limitações, plano de ação e fontes |
| [DEPENDENCY_INVENTORY.md](DEPENDENCY_INVENTORY.md) | Inventário consolidado; a tabela completa fica em [data/](data/) |
| [LICENSE_RISK_REGISTER.md](LICENSE_RISK_REGISTER.md) | Riscos classificados com evidência, impacto, ação e prioridade |
| [DISTRIBUTION_AUDIT.md](DISTRIBUTION_AUDIT.md) | O que cada instalador leva e o que o Spock pode fazer com cada componente |
| [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) | Rascunho dos avisos de terceiros (textos originais dos pacotes) e [notices/](notices/) |
| [LICENSE_DECISIONS.md](LICENSE_DECISIONS.md) | Decisões adotadas e decisões que aguardam aprovação |
| [SBOM/](SBOM/) | SPDX 2.3 e CycloneDX 1.6: `spock-windows-x64`, `spock-linux-x64` e o consolidado `spock` |

## Regenerar e validar

```bash
python docs/compliance/tools/generate_sbom.py
```

```bash
python docs/compliance/tools/generate_sbom.py --check
```

```bash
dotnet run docs/compliance/tools/validate_sbom.cs -- <pasta-com-os-schemas> docs/compliance/SBOM/spock.cdx.json docs/compliance/SBOM/spock.spdx.json
```

O gerador não instala nada. Ele lê:

- os lockfiles do repositório;
- os metadados que o pnpm, o Cargo, o NuGet e o pip já deixaram na máquina;
- os recursos do último build local.

Os componentes fora de gerenciador (binários, ferramentas sob demanda, serviços, ativos) estão na tabela `manual()` do script,
com a evidência de cada um. Atualize essa tabela quando mudar o empacotamento. Os schemas oficiais a baixar para a validação
estão listados no cabeçalho de `validate_sbom.cs`. `tools/spdx-license-ids.json` é a lista de ids SPDX extraída do schema do
CycloneDX 1.6.
