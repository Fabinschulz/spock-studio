# D-06: formato do pacote Linux (relatório técnico)

Data: 2026-11-16. **Decisão do proprietário em 2026-11-16: só `.deb` na 1ª versão.** O CI passou a gerar `bundles: deb`.

## Fatos

- **CI:** gera `bundles: appimage,deb` no `ubuntu-22.04`, após instalar `libwebkit2gtk-4.1-dev`, `libayatana-appindicator3-dev`,
  `librsvg2-dev` e afins (`.github/workflows/ci.yml:79,107`).
- **`.deb`:** o tauri-bundler declara as bibliotecas do motor web como dependências do pacote (`libwebkit2gtk-4.1-0`,
  `libgtk-3-0`). O `tauri.conf.json` não muda esse padrão. Quem instala é o gerenciador da distribuição.
- **AppImage:** o tauri-bundler (`crates/tauri-bundler/src/bundle/linux/appimage/linuxdeploy.rs`) copia os processos
  `WebKitNetworkProcess`, `WebKitWebProcess` e `libwebkit2gtkinjectedbundle.so` e roda `linuxdeploy --plugin gtk`. Esse plugin
  embute GTK 3, GLib, GIO, Pango, Cairo, GdkPixbuf, libsoup, WebKitGTK e dependências. A glibc não é embutida. Com
  `bundleMediaFramework`, que está desligado no projeto, entraria também o GStreamer.
- **Em comum:** os dois formatos levam o mesmo host, o sidecar .NET, o Python do PBS com as licenças em `python/licenses/` e a
  inteligência.
- **Limitação:** nenhum AppImage foi gerado nem inspecionado nesta etapa. A lista exata de bibliotecas embutidas e suas
  licenças precisa sair do artefato (seção "Como inventariar").

## Comparação

| Critério | `.deb` | AppImage com conformidade LGPL |
| --- | --- | --- |
| Bibliotecas de terceiros redistribuídas pelo Spock | Só as do produto: host, sidecar, Python e inteligência | As do `.deb` mais WebKitGTK, GTK, GLib, libsoup, Pango, Cairo e outras (LGPL-2.0+/2.1+, MPL, MIT…) |
| Dependências do sistema | `libwebkit2gtk-4.1-0` e `libgtk-3-0`, resolvidas pelo apt | Nenhuma além da glibc e do FUSE (ou `--appimage-extract-and-run`) |
| Distribuições atendidas | Debian, Ubuntu e derivadas | Qualquer distribuição x86_64 com glibc compatível |
| Obrigações de licença | Nenhuma nova: as bibliotecas LGPL não são redistribuídas | Textos LGPL; código-fonte correspondente de cada biblioteca, ou oferta escrita válida por 3 anos (LGPL-2.1 §6); bibliotecas substituíveis (já são `.so`); EULA sem proibição de engenharia reversa para depurar essas bibliotecas |
| Atualizações de segurança do motor web | O sistema atualiza o WebKitGTK (frequente: CVEs do WebKit) | O Spock precisa publicar novo AppImage a cada correção relevante do WebKitGTK/GTK |
| Custo de manutenção | Baixo | Alto: inventário por release, arquivo de fontes, oferta, acompanhamento de CVEs |
| Risco de licença | Baixo | Médio, gerenciável, mas exige processo |
| Impacto no usuário | Instalação por `apt`/`dpkg`, limitada a Debian e derivadas | Um arquivo portátil |

## Recomendação (não aplicada)

Distribuir só o `.deb` na primeira versão comercial e retirar `appimage` de `bundles` quando a decisão for aprovada.

- **Por quê:** elimina a redistribuição de LGPL, deixa a segurança do WebKitGTK com a distribuição e não exige arquivo de
  fontes.
- **AppImage depois:** se a portabilidade se tornar requisito, ele pode voltar com o processo descrito abaixo.

## Como inventariar o AppImage (se for mantido)

1. No runner Linux, depois do `tauri build`, rodar `./<app>.AppImage --appimage-extract` e listar `squashfs-root/usr/lib/**`.
2. Mapear cada `.so` para o pacote Debian de origem com `dpkg -S` e obter versão e licença com `/usr/share/doc/<pacote>/copyright`.
3. Para cada biblioteca LGPL, arquivar o pacote de fontes (`apt-get source`) da versão exata, junto da release, e incluir os
   textos em `licenses/` do pacote.
4. Acrescentar ao `generate_sbom.py` uma etapa que leia essa lista, para que o SBOM do Linux deixe de ter essa lacuna.
