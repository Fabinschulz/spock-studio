# Distribuição da primeira versão

Decisão de 2026-11-05: a primeira versão do Mariliz Studio sai para **Windows** e, se der, **Linux**. **macOS fica de fora**
(sem Mac para validar e sem conta Apple Developer para assinar e notarizar). Nenhum instalador é assinado nesta versão.

## O que cada sistema exige

| Sistema | Instalador | Certificado ou licença para rodar | Observação |
| --- | --- | --- | --- |
| Windows x64 | NSIS (`*-setup.exe`), instalação por usuário | Nenhum | O SmartScreen avisa sobre editor desconhecido (abaixo) |
| Linux x64 | `.deb` e AppImage | Nenhum | Precisa do WebKitGTK 4.1 no sistema; o AppImage reduz o problema |
| macOS | — | Conta Apple Developer (paga) para assinar e notarizar | Fora desta versão; ver "Para voltar ao macOS" |

Os componentes embarcados (Tauri, .NET, Python, LangGraph) são de licença permissiva e não pedem licença paga para
redistribuir. Um arquivo com os avisos de licença de terceiros dentro do instalador ainda não existe.

## O aviso do SmartScreen (Windows)

Sem assinatura, o Windows mostra "O Windows protegeu seu computador" na primeira execução do instalador. A pessoa escolhe
**Mais informações** e depois **Executar assim mesmo**. Só um certificado de assinatura de código remove ou reduz o aviso
(certificado tradicional ou serviço de assinatura em nuvem, ambos pagos). Vale decidir quando houver usuários fora da equipe.

## Conferir o arquivo baixado

A CI grava `SHA256SUMS.txt` ao lado dos instaladores (`scripts/write-checksums.py`), no formato do `sha256sum`.

```powershell
# Windows
(Get-FileHash -Algorithm SHA256 .\Mariliz*-setup.exe).Hash.ToLower()   # compare com a linha do SHA256SUMS.txt
```

```bash
# Linux
sha256sum -c SHA256SUMS.txt --ignore-missing
```

O hash prova que o arquivo é o que a CI gerou, desde que o `SHA256SUMS.txt` venha de um canal confiável. Não substitui a assinatura.

## O que a CI prova

Cada instalador é aberto (`scripts/smoke-installer.py`) e o grafo roda com o Python que está dentro dele (`PackagedAiSmokeTests`,
obrigatório na CI e conferido no `.trx`). Validado até agora: o NSIS real no Windows, a extração do `.deb` com caminho com espaço no
WSL e o vigia de processo no Linux. O GitHub Actions do repositório está sem orçamento, então o resto só roda quando isso for resolvido.

## Para voltar ao macOS

1. Reincluir `aarch64-apple-darwin` (`bundles: dmg`) na matriz do `ci.yml`.
2. Assinar **todos** os Mach-O de `resources/` (Python e `.so`/`.dylib` das dependências) com hardened runtime e entitlements antes do `tauri build`;
   o Tauri assina o app e o sidecar, mas não os recursos, e a notarização recusaria o Python sem assinatura.
3. Cadastrar os segredos `APPLE_*` no repositório (certificado Developer ID e chave de API do App Store Connect) e notarizar.
