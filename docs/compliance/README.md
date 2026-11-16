# Conformidade de licenciamento

Auditoria de licenças, dependências e SBOM do SPOCK Studio (System of Programming, Organization, Code and Knowledge),
feita em 2026-11-15 para distribuição como software proprietário, e implementação das decisões adotadas em 2026-11-16. É uma
auditoria técnica, não um parecer jurídico.

| Documento | Para que serve |
| --- | --- |
| [LICENSE_AUDIT.md](LICENSE_AUDIT.md) | Resultado geral: resumo executivo, critérios, arquitetura, Terraform/OpenTofu, limitações, plano de ação e fontes |
| [IMPLEMENTATION_STATUS.md](IMPLEMENTATION_STATUS.md) | Matriz das decisões, estado no código, evidências e testes da implementação |
| [DEPENDENCY_INVENTORY.md](DEPENDENCY_INVENTORY.md) | Inventário consolidado; a tabela completa fica em [data/](data/) |
| [LICENSE_RISK_REGISTER.md](LICENSE_RISK_REGISTER.md) | Riscos classificados com evidência, impacto, ação e prioridade |
| [DISTRIBUTION_AUDIT.md](DISTRIBUTION_AUDIT.md) | O que cada instalador leva e o que o Spock pode fazer com cada componente |
| [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) | Rascunho dos avisos de terceiros (textos originais dos pacotes) e [notices/](notices/) |
| [LICENSE_DECISIONS.md](LICENSE_DECISIONS.md) | Decisões adotadas e decisões que aguardam aprovação |
| [LINUX_DISTRIBUTION_REPORT.md](LINUX_DISTRIBUTION_REPORT.md) | D-06: `.deb` × AppImage com conformidade LGPL |
| [JSON_SCHEMA_VALIDATOR_REPORT.md](JSON_SCHEMA_VALIDATOR_REPORT.md) | D-11: JsonSchema.Net e o EULA de taxa de manutenção |
| [TRADEMARK_ASSESSMENT.md](TRADEMARK_ASSESSMENT.md) | D-12: escopo da avaliação da marca SPOCK |
| [license-policy.json](license-policy.json) | Proposta de política de licenças (D-10, não aprovada) usada pelo `--policy` |
| [SBOM/](SBOM/) | SPDX 2.3 e CycloneDX 1.6: `spock-windows-x64`, `spock-linux-x64` e o consolidado `spock` |

Textos de licença que não vêm em nenhum pacote e vão junto do Python do Linux: [`scripts/notices/`](../../scripts/notices/).

## Regenerar, verificar e validar

Inventário, SBOMs e avisos:

```bash
python docs/compliance/tools/generate_sbom.py
```

O inventário bate com os lockfiles? (o CI roda isto):

```bash
python docs/compliance/tools/generate_sbom.py --check
```

Política de licenças, em modo relatório (`--enforce` falha; usar só depois de aprovar a D-10):

```bash
python docs/compliance/tools/generate_sbom.py --policy
```

Avisos prontos para recurso do Tauri (D-07 aguarda aprovação; `--strict` falha se houver lacuna):

```bash
python docs/compliance/tools/generate_sbom.py --bundle desktop/host/src-tauri/resources/notices --strict
```

Textos de licença dos pacotes que não trazem o arquivo, baixados dos repositórios oficiais e fixados em commit
(`notices/upstream/`, procedência em `sources.json`):

```bash
python docs/compliance/tools/fetch_upstream_licenses.py
```

Testes do gerador:

```bash
python -m unittest discover -s docs/compliance/tools -p "test_*.py" -v
```

Validação dos SBOMs contra os schemas oficiais:

```bash
dotnet run docs/compliance/tools/validate_sbom.cs -- <pasta-com-os-schemas> docs/compliance/SBOM/spock.cdx.json docs/compliance/SBOM/spock.spdx.json
```

O gerador não instala nada. Ele lê:

- os lockfiles do repositório;
- os metadados que o pnpm, o Cargo, o NuGet e o pip já deixaram na máquina (se o Cargo não tiver os crates de um alvo em
  cache, ele os baixa com `--locked`);
- os recursos do último build local.

Os componentes fora de gerenciador (binários, ferramentas sob demanda, serviços, ativos) estão na tabela `manual()` do script,
com a evidência de cada um. Atualize essa tabela quando o empacotamento mudar. Os schemas oficiais a baixar para a validação
estão listados no cabeçalho de `validate_sbom.cs`. `tools/spdx-license-ids.json` é a lista de ids SPDX extraída do schema do
CycloneDX 1.6.

## Build comercial

Até a D-02 ser decidida, o motor do instalador comercial deve ser publicado com a edição `commercial`, que não instala o
Terraform pelo Spock:

```powershell
./scripts/build-runtime.ps1 -Rid win-x64 -Edition commercial -FetchPythonEmbed
```

Em outras plataformas, acrescente `-p:SpockEdition=commercial` ao `dotnet publish`. Sem a propriedade, o build é
`development` e nada fica travado.
