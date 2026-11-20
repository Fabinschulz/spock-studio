# Componentes distribuídos (gerado)

Gerado por `docs/compliance/tools/generate_sbom.py`; não edite à mão. Campos completos (origem, obrigações, evidências,
hashes) em `inventory.csv` / `inventory.json`. W = Windows x64, L = Linux x64.

| Componente | Versão | Ecossistema | Dep. | W | L | Licença declarada | Conferência do arquivo | Risco |
| --- | --- | --- | --- | :-: | :-: | --- | --- | --- |
| Chaves públicas OpenPGP de releases (HashiCorp, OpenTofu) | embutidas | asset | direta | x | x | NOASSERTION | CONFERIDO MANUALMENTE | BAIXO |
| Conteúdo de agentes e skills adaptado de harness-engineering | embutido | asset | direta | x | x | MIT | CONFERIDO MANUALMENTE | BAIXO |
| Ícones e logotipo Spock Studio | 0.1.0 | asset | direta | x | x | LicenseRef-Proprietary-Spock | CONFERIDO MANUALMENTE | PENDENTE |
| Berkeley DB (dentro do python-build-standalone, módulo _dbm) | 6.0.19 | binário | direta |  | x | Sleepycat | CONFERIDO MANUALMENTE | MÉDIO |
| CPython (python-build-standalone, install_only_stripped) | 3.12.16+20261110 | binário | direta |  | x | Python-2.0 | CONFERIDO MANUALMENTE | BAIXO |
| CPython (python-build-standalone, install_only_stripped, Windows) | 3.12.16+20261110 | binário | direta | x |  | Python-2.0 | CONFERIDO MANUALMENTE | MÉDIO |
| Microsoft Visual C++ Runtime (vcruntime140.dll, vcruntime140_1.dll) | 14.x | binário | direta | x |  | LicenseRef-Microsoft-Visual-Cpp-Redistributable | CONFERIDO MANUALMENTE | MÉDIO |
| NSIS (stub do instalador e plugins) | 3.x | binário | direta | x |  | Zlib AND bzip2-1.0.6 AND CPL-1.0 | CONFERIDO MANUALMENTE | BAIXO |
| nsis_tauri_utils (plugin NSIS do Tauri) | conforme tauri-bundler | binário | direta | x |  | MIT OR Apache-2.0 | CONFERIDO MANUALMENTE | BAIXO |
| OpenSSL (dentro do CPython) | 3.5.9 | binário | direta | x |  | Apache-2.0 | CONFERIDO MANUALMENTE | BAIXO |
| aho-corasick | 1.1.5 | cargo | transitiva | x | x | Unlicense OR MIT | CONFERE | BAIXO |
| alloc-no-stdlib | 3.0.0 | cargo | transitiva | x | x | BSD-3-Clause | CONFERE | BAIXO |
| alloc-stdlib | 0.3.0 | cargo | transitiva | x | x | BSD-3-Clause | SEM ARQUIVO | BAIXO |
| anyhow | 1.0.104 | cargo | transitiva | x | x | MIT OR Apache-2.0 | CONFERE | BAIXO |
| async-broadcast | 0.7.2 | cargo | transitiva |  | x | MIT OR Apache-2.0 | CONFERE | BAIXO |
| async-channel | 2.5.0 | cargo | transitiva |  | x | Apache-2.0 OR MIT | CONFERE | BAIXO |
| async-executor | 1.14.0 | cargo | transitiva |  | x | Apache-2.0 OR MIT | CONFERE | BAIXO |
| async-io | 2.6.0 | cargo | transitiva |  | x | Apache-2.0 OR MIT | CONFERE | BAIXO |
| async-lock | 3.4.2 | cargo | transitiva |  | x | Apache-2.0 OR MIT | CONFERE | BAIXO |
| async-process | 2.5.0 | cargo | transitiva |  | x | Apache-2.0 OR MIT | CONFERE | BAIXO |
| async-signal | 0.2.14 | cargo | transitiva |  | x | Apache-2.0 OR MIT | CONFERE | BAIXO |
| async-task | 4.7.1 | cargo | transitiva |  | x | Apache-2.0 OR MIT | CONFERE | BAIXO |
| atk | 0.18.2 | cargo | transitiva |  | x | MIT | CONFERE | BAIXO |
| atk-sys | 0.18.2 | cargo | transitiva |  | x | MIT | CONFERE | BAIXO |
| atomic-waker | 1.1.2 | cargo | transitiva |  | x | Apache-2.0 OR MIT | CONFERE | BAIXO |
| base64 | 0.23.1 | cargo | transitiva | x | x | MIT OR Apache-2.0 | CONFERE | BAIXO |
| bit-set | 0.8.0 | cargo | transitiva | x | x | Apache-2.0 OR MIT | CONFERE | BAIXO |
| bit-vec | 0.8.0 | cargo | transitiva | x | x | Apache-2.0 OR MIT | CONFERE | BAIXO |
| bitflags | 1.3.2 | cargo | transitiva | x | x | MIT/Apache-2.0 | CONFERE | BAIXO |
| bitflags | 2.13.2 | cargo | transitiva | x | x | MIT OR Apache-2.0 | CONFERE | BAIXO |
| blocking | 1.7.0 | cargo | transitiva |  | x | Apache-2.0 OR MIT | CONFERE | BAIXO |
| brotli | 9.1.0 | cargo | transitiva | x | x | BSD-3-Clause AND MIT | CONFERE | BAIXO |
| brotli-decompressor | 6.2.0 | cargo | transitiva | x | x | BSD-3-Clause/MIT | CONFERE | BAIXO |
| bs58 | 0.5.1 | cargo | transitiva | x | x | MIT/Apache-2.0 | CONFERE | BAIXO |
| bytes | 1.12.1 | cargo | transitiva | x | x | MIT | CONFERE | BAIXO |
| cairo-rs | 0.18.5 | cargo | transitiva |  | x | MIT | CONFERE | BAIXO |
| cairo-sys-rs | 0.18.2 | cargo | transitiva |  | x | MIT | CONFERE | BAIXO |
| camino | 1.2.6 | cargo | transitiva | x | x | MIT OR Apache-2.0 | CONFERE | BAIXO |
| cargo-platform | 0.1.9 | cargo | transitiva | x | x | MIT OR Apache-2.0 | CONFERE | BAIXO |
| cargo_metadata | 0.19.2 | cargo | transitiva | x | x | MIT | CONFERE | BAIXO |
| cfb | 0.14.0 | cargo | transitiva | x | x | MIT | CONFERE | BAIXO |
| cfg-if | 1.0.5 | cargo | transitiva | x | x | MIT OR Apache-2.0 | CONFERE | BAIXO |
| chrono | 0.4.45 | cargo | transitiva | x | x | MIT OR Apache-2.0 | CONFERE | BAIXO |
| concurrent-queue | 2.5.0 | cargo | transitiva |  | x | Apache-2.0 OR MIT | CONFERE | BAIXO |
| cookie | 0.18.2 | cargo | transitiva | x | x | MIT OR Apache-2.0 | CONFERE | BAIXO |
| core_detect | 1.0.0 | cargo | transitiva | x | x | MIT/Apache-2.0 | CONFERE | BAIXO |
| crossbeam-channel | 0.5.17 | cargo | transitiva | x | x | MIT OR Apache-2.0 | DIVERGE (Apache-2.0, BSD-3-Clause, MIT) | BAIXO |
| crossbeam-utils | 0.8.23 | cargo | transitiva | x | x | MIT OR Apache-2.0 | CONFERE | BAIXO |
| cssparser | 0.37.0 | cargo | transitiva | x | x | MPL-2.0 | CONFERE | MÉDIO |
| ctor | 1.0.13 | cargo | transitiva | x | x | Apache-2.0 OR MIT | CONFERE | BAIXO |
| dbus | 0.9.12 | cargo | transitiva |  | x | Apache-2.0/MIT | CONFERE | BAIXO |
| defmt | 1.2.0 | cargo | transitiva | x | x | MIT OR Apache-2.0 | CONFERE | BAIXO |
| deranged | 0.5.9 | cargo | transitiva | x | x | MIT OR Apache-2.0 | CONFERE | BAIXO |
| derive_more | 2.1.1 | cargo | transitiva | x | x | MIT | CONFERE | BAIXO |
| dirs | 7.0.0 | cargo | transitiva | x | x | MIT OR Apache-2.0 | CONFERE | BAIXO |
| dirs-sys | 0.5.0 | cargo | transitiva | x | x | MIT OR Apache-2.0 | CONFERE | BAIXO |
| dlopen2 | 0.8.2 | cargo | transitiva |  | x | MIT | SEM ARQUIVO | BAIXO |
| dom_query | 0.28.0 | cargo | transitiva | x | x | MIT | CONFERE | BAIXO |
| downcast-rs | 1.2.1 | cargo | transitiva | x | x | MIT/Apache-2.0 | CONFERE | BAIXO |
| dpi | 0.1.2 | cargo | transitiva | x | x | Apache-2.0 AND MIT | CONFERE | BAIXO |
| dtoa | 1.0.11 | cargo | transitiva | x | x | MIT OR Apache-2.0 | CONFERE | BAIXO |
| dtoa-short | 0.3.5 | cargo | transitiva | x | x | MPL-2.0 | CONFERE | MÉDIO |
| dunce | 1.0.5 | cargo | transitiva | x | x | CC0-1.0 OR MIT-0 OR Apache-2.0 | CONFERE | BAIXO |
| dyn-clone | 1.0.20 | cargo | transitiva | x | x | MIT OR Apache-2.0 | CONFERE | BAIXO |
| encoding_rs | 0.8.42 | cargo | transitiva | x | x | (Apache-2.0 OR MIT) AND BSD-3-Clause | CONFERE | BAIXO |
| endi | 1.1.1 | cargo | transitiva |  | x | MIT | CONFERE | BAIXO |
| enumflags2 | 0.7.12 | cargo | transitiva |  | x | MIT OR Apache-2.0 | CONFERE | BAIXO |
| equivalent | 1.0.3 | cargo | transitiva | x | x | Apache-2.0 OR MIT | CONFERE | BAIXO |
| erased-serde | 0.4.11 | cargo | transitiva | x | x | MIT OR Apache-2.0 | CONFERE | BAIXO |
| errno | 0.3.14 | cargo | transitiva |  | x | MIT OR Apache-2.0 | CONFERE | BAIXO |
| event-listener | 5.4.2 | cargo | transitiva |  | x | Apache-2.0 OR MIT | CONFERE | BAIXO |
| event-listener-strategy | 0.5.4 | cargo | transitiva |  | x | Apache-2.0 OR MIT | CONFERE | BAIXO |
| field-offset | 0.3.6 | cargo | transitiva |  | x | MIT OR Apache-2.0 | CONFERE | BAIXO |
| filedescriptor | 0.8.3 | cargo | transitiva | x | x | MIT | CONFERE | BAIXO |
| fnv | 1.0.7 | cargo | transitiva | x | x | Apache-2.0 / MIT | CONFERE | BAIXO |
| foldhash | 0.2.0 | cargo | transitiva | x | x | Zlib | CONFERE | BAIXO |
| form_urlencoded | 1.2.2 | cargo | transitiva | x | x | MIT OR Apache-2.0 | CONFERE | BAIXO |
| futures-channel | 0.3.34 | cargo | transitiva |  | x | MIT OR Apache-2.0 | CONFERE | BAIXO |
| futures-core | 0.3.34 | cargo | transitiva |  | x | MIT OR Apache-2.0 | CONFERE | BAIXO |
| futures-executor | 0.3.34 | cargo | transitiva |  | x | MIT OR Apache-2.0 | CONFERE | BAIXO |
| futures-io | 0.3.34 | cargo | transitiva |  | x | MIT OR Apache-2.0 | CONFERE | BAIXO |
| futures-lite | 2.6.1 | cargo | transitiva |  | x | Apache-2.0 OR MIT | CONFERE | BAIXO |
| futures-sink | 0.3.34 | cargo | transitiva |  | x | MIT OR Apache-2.0 | CONFERE | BAIXO |
| futures-task | 0.3.34 | cargo | transitiva |  | x | MIT OR Apache-2.0 | CONFERE | BAIXO |
| futures-util | 0.3.34 | cargo | transitiva |  | x | MIT OR Apache-2.0 | CONFERE | BAIXO |
| gdk | 0.18.2 | cargo | transitiva |  | x | MIT | CONFERE | BAIXO |
| gdk-pixbuf | 0.18.5 | cargo | transitiva |  | x | MIT | CONFERE | BAIXO |
| gdk-pixbuf-sys | 0.18.0 | cargo | transitiva |  | x | MIT | CONFERE | BAIXO |
| gdk-sys | 0.18.2 | cargo | transitiva |  | x | MIT | CONFERE | BAIXO |
| gdkwayland-sys | 0.18.2 | cargo | transitiva |  | x | MIT | CONFERE | BAIXO |
| gdkx11 | 0.18.2 | cargo | transitiva |  | x | MIT | CONFERE | BAIXO |
| gdkx11-sys | 0.18.2 | cargo | transitiva |  | x | MIT | CONFERE | BAIXO |
| getrandom | 0.3.4 | cargo | transitiva | x | x | MIT OR Apache-2.0 | CONFERE | BAIXO |
| getrandom | 0.4.3 | cargo | transitiva | x | x | MIT OR Apache-2.0 | CONFERE | BAIXO |
| gio | 0.18.4 | cargo | transitiva |  | x | MIT | CONFERE | BAIXO |
| gio-sys | 0.18.1 | cargo | transitiva |  | x | MIT | CONFERE | BAIXO |
| glib | 0.18.5 | cargo | transitiva |  | x | MIT | CONFERE | BAIXO |
| glib-sys | 0.18.1 | cargo | transitiva |  | x | MIT | CONFERE | BAIXO |
| glob | 0.3.4 | cargo | transitiva | x | x | MIT OR Apache-2.0 | CONFERE | BAIXO |
| gobject-sys | 0.18.0 | cargo | transitiva |  | x | MIT | CONFERE | BAIXO |
| gtk | 0.18.2 | cargo | transitiva |  | x | MIT | CONFERE | BAIXO |
| gtk-sys | 0.18.2 | cargo | transitiva |  | x | MIT | CONFERE | BAIXO |
| hashbrown | 0.12.3 | cargo | transitiva | x | x | MIT OR Apache-2.0 | CONFERE | BAIXO |
| hashbrown | 0.17.1 | cargo | transitiva | x | x | MIT OR Apache-2.0 | CONFERE | BAIXO |
| heck | 0.5.0 | cargo | transitiva | x | x | MIT OR Apache-2.0 | CONFERE | BAIXO |
| hex | 0.4.3 | cargo | transitiva | x | x | MIT OR Apache-2.0 | CONFERE | BAIXO |
| html5ever | 0.39.0 | cargo | transitiva | x | x | MIT OR Apache-2.0 | CONFERE | BAIXO |
| http | 1.5.0 | cargo | transitiva | x | x | MIT OR Apache-2.0 | CONFERE | BAIXO |
| iana-time-zone | 0.1.65 | cargo | transitiva |  | x | MIT OR Apache-2.0 | CONFERE | BAIXO |
| icu_collections | 2.4.0 | cargo | transitiva | x | x | Unicode-3.0 | CONFERE | BAIXO |
| icu_locale_core | 2.4.0 | cargo | transitiva | x | x | Unicode-3.0 | CONFERE | BAIXO |
| icu_normalizer | 2.4.0 | cargo | transitiva | x | x | Unicode-3.0 | CONFERE | BAIXO |
| icu_normalizer_data | 2.4.0 | cargo | transitiva | x | x | Unicode-3.0 | CONFERE | BAIXO |
| icu_properties | 2.4.0 | cargo | transitiva | x | x | Unicode-3.0 | CONFERE | BAIXO |
| icu_properties_data | 2.4.0 | cargo | transitiva | x | x | Unicode-3.0 | CONFERE | BAIXO |
| icu_provider | 2.4.0 | cargo | transitiva | x | x | Unicode-3.0 | CONFERE | BAIXO |
| idna | 1.1.0 | cargo | transitiva | x | x | MIT OR Apache-2.0 | CONFERE | BAIXO |
| idna_adapter | 1.2.2 | cargo | transitiva | x | x | Apache-2.0 OR MIT | CONFERE | BAIXO |
| indexmap | 1.9.3 | cargo | transitiva | x | x | Apache-2.0 OR MIT | CONFERE | BAIXO |
| indexmap | 2.14.2 | cargo | transitiva | x | x | Apache-2.0 OR MIT | CONFERE | BAIXO |
| infer | 0.22.0 | cargo | transitiva | x | x | MIT | CONFERE | BAIXO |
| is-docker | 0.2.0 | cargo | transitiva |  | x | MIT | CONFERE | BAIXO |
| is-wsl | 0.4.0 | cargo | transitiva |  | x | MIT | CONFERE | BAIXO |
| itoa | 1.0.18 | cargo | transitiva | x | x | MIT OR Apache-2.0 | CONFERE | BAIXO |
| javascriptcore-rs | 1.1.2 | cargo | transitiva |  | x | MIT | CONFERE | BAIXO |
| javascriptcore-rs-sys | 1.1.1 | cargo | transitiva |  | x | MIT | CONFERE | BAIXO |
| jiff | 0.2.38 | cargo | transitiva | x | x | Unlicense OR MIT | CONFERE | BAIXO |
| jiff-core | 0.1.1 | cargo | transitiva | x | x | Unlicense OR MIT | CONFERE | BAIXO |
| jiff-tzdb | 0.1.9 | cargo | transitiva | x |  | Unlicense OR MIT | CONFERE | BAIXO |
| jiff-tzdb-platform | 0.1.3 | cargo | transitiva | x |  | Unlicense OR MIT | CONFERE | BAIXO |
| json-patch | 4.2.0 | cargo | transitiva | x | x | MIT/Apache-2.0 | CONFERE | BAIXO |
| jsonptr | 0.7.1 | cargo | transitiva | x | x | MIT OR Apache-2.0 | CONFERE | BAIXO |
| keyboard-types | 0.8.3 | cargo | transitiva | x | x | MIT OR Apache-2.0 | CONFERE | BAIXO |
| lazy_static | 1.5.1 | cargo | transitiva | x |  | MIT OR Apache-2.0 | CONFERE | BAIXO |
| libappindicator | 0.9.0 | cargo | transitiva |  | x | Apache-2.0 OR MIT | CONFERE | BAIXO |
| libappindicator-sys | 0.9.0 | cargo | transitiva |  | x | Apache-2.0 OR MIT | SEM ARQUIVO | BAIXO |
| libc | 0.2.191 | cargo | transitiva | x | x | MIT OR Apache-2.0 | CONFERE | BAIXO |
| libdbus-sys | 0.2.7 | cargo | transitiva |  | x | Apache-2.0/MIT | CONFERE | BAIXO |
| libloading | 0.7.4 | cargo | transitiva |  | x | ISC | CONFERE | BAIXO |
| linux-raw-sys | 0.12.1 | cargo | transitiva |  | x | Apache-2.0 WITH LLVM-exception OR Apache-2.0 OR MIT | CONFERE | BAIXO |
| litemap | 0.8.3 | cargo | transitiva | x | x | Unicode-3.0 | CONFERE | BAIXO |
| lock_api | 0.4.14 | cargo | transitiva | x | x | MIT OR Apache-2.0 | CONFERE | BAIXO |
| log | 0.4.34 | cargo | transitiva | x | x | MIT OR Apache-2.0 | CONFERE | BAIXO |
| markup5ever | 0.39.0 | cargo | transitiva | x | x | MIT OR Apache-2.0 | CONFERE | BAIXO |
| memchr | 2.8.3 | cargo | transitiva | x | x | Unlicense OR MIT | CONFERE | BAIXO |
| memoffset | 0.9.1 | cargo | transitiva |  | x | MIT | CONFERE | BAIXO |
| mime | 0.3.17 | cargo | transitiva | x | x | MIT OR Apache-2.0 | CONFERE | BAIXO |
| mio | 1.2.4 | cargo | transitiva | x | x | MIT | CONFERE | BAIXO |
| muda | 0.20.0 | cargo | transitiva | x | x | Apache-2.0 OR MIT | CONFERE | BAIXO |
| new_debug_unreachable | 1.0.6 | cargo | transitiva | x | x | MIT | CONFERE | BAIXO |
| nix | 0.28.0 | cargo | transitiva | x | x | MIT | CONFERE | BAIXO |
| num-conv | 0.2.2 | cargo | transitiva | x | x | MIT OR Apache-2.0 | CONFERE | BAIXO |
| num-traits | 0.2.19 | cargo | transitiva | x | x | MIT OR Apache-2.0 | CONFERE | BAIXO |
| once_cell | 1.21.4 | cargo | transitiva | x | x | MIT OR Apache-2.0 | CONFERE | BAIXO |
| open | 5.4.5 | cargo | transitiva | x | x | MIT | CONFERE | BAIXO |
| option-ext | 0.2.0 | cargo | transitiva | x | x | MPL-2.0 | CONFERE | MÉDIO |
| ordered-stream | 0.2.0 | cargo | transitiva |  | x | MIT OR Apache-2.0 | CONFERE | BAIXO |
| os_pipe | 1.2.3 | cargo | transitiva | x | x | MIT | CONFERE | BAIXO |
| pango | 0.18.3 | cargo | transitiva |  | x | MIT | CONFERE | BAIXO |
| pango-sys | 0.18.0 | cargo | transitiva |  | x | MIT | CONFERE | BAIXO |
| parking | 2.2.1 | cargo | transitiva |  | x | Apache-2.0 OR MIT | CONFERE | BAIXO |
| parking_lot | 0.12.5 | cargo | transitiva | x | x | MIT OR Apache-2.0 | CONFERE | BAIXO |
| parking_lot_core | 0.9.12 | cargo | transitiva | x | x | MIT OR Apache-2.0 | CONFERE | BAIXO |
| percent-encoding | 2.3.2 | cargo | transitiva | x | x | MIT OR Apache-2.0 | CONFERE | BAIXO |
| phf | 0.13.1 | cargo | transitiva | x | x | MIT | CONFERE | BAIXO |
| phf_shared | 0.13.1 | cargo | transitiva | x | x | MIT | CONFERE | BAIXO |
| pin-project-lite | 0.2.17 | cargo | transitiva | x | x | Apache-2.0 OR MIT | CONFERE | BAIXO |
| piper | 0.2.5 | cargo | transitiva |  | x | MIT OR Apache-2.0 | CONFERE | BAIXO |
| plist | 1.10.3 | cargo | transitiva | x | x | MIT | CONFERE | BAIXO |
| polling | 3.11.0 | cargo | transitiva |  | x | Apache-2.0 OR MIT | CONFERE | BAIXO |
| portable-pty | 0.9.0 | cargo | direta | x | x | MIT | CONFERE | BAIXO |
| potential_utf | 0.1.6 | cargo | transitiva | x | x | Unicode-3.0 | CONFERE | BAIXO |
| powerfmt | 0.2.1 | cargo | transitiva | x | x | MIT OR Apache-2.0 | CONFERE | BAIXO |
| precomputed-hash | 0.1.1 | cargo | transitiva | x | x | MIT | CONFERE | BAIXO |
| proc-macro2 | 1.0.107 | cargo | transitiva | x | x | MIT OR Apache-2.0 | CONFERE | BAIXO |
| quick-xml | 0.42.0 | cargo | transitiva | x | x | MIT | CONFERE | BAIXO |
| quote | 1.0.47 | cargo | transitiva | x | x | MIT OR Apache-2.0 | CONFERE | BAIXO |
| raw-window-handle | 0.6.2 | cargo | transitiva | x | x | MIT OR Apache-2.0 OR Zlib | CONFERE | BAIXO |
| ref-cast | 1.0.27 | cargo | transitiva | x | x | MIT OR Apache-2.0 | CONFERE | BAIXO |
| regex | 1.13.1 | cargo | transitiva | x | x | MIT OR Apache-2.0 | CONFERE | BAIXO |
| regex-automata | 0.4.18 | cargo | transitiva | x | x | MIT OR Apache-2.0 | CONFERE | BAIXO |
| regex-syntax | 0.8.11 | cargo | transitiva | x | x | MIT OR Apache-2.0 | CONFERE | BAIXO |
| rfd | 0.17.2 | cargo | transitiva | x | x | MIT | CONFERE | BAIXO |
| rustc-hash | 2.1.3 | cargo | transitiva | x | x | Apache-2.0 OR MIT | CONFERE | BAIXO |
| rustix | 1.1.5 | cargo | transitiva |  | x | Apache-2.0 WITH LLVM-exception OR Apache-2.0 OR MIT | CONFERE | BAIXO |
| same-file | 1.0.6 | cargo | transitiva | x | x | Unlicense/MIT | CONFERE | BAIXO |
| schemars | 0.8.22 | cargo | transitiva | x | x | MIT | CONFERE | BAIXO |
| schemars | 0.9.0 | cargo | transitiva | x | x | MIT | CONFERE | BAIXO |
| schemars | 1.2.3 | cargo | transitiva | x | x | MIT | CONFERE | BAIXO |
| scopeguard | 1.2.0 | cargo | transitiva | x | x | MIT OR Apache-2.0 | CONFERE | BAIXO |
| selectors | 0.38.0 | cargo | transitiva | x | x | MPL-2.0 | SEM ARQUIVO | MÉDIO |
| semver | 1.0.28 | cargo | transitiva | x | x | MIT OR Apache-2.0 | CONFERE | BAIXO |
| serde | 1.0.229 | cargo | direta | x | x | MIT OR Apache-2.0 | CONFERE | BAIXO |
| serde-untagged | 0.1.9 | cargo | transitiva | x | x | MIT OR Apache-2.0 | CONFERE | BAIXO |
| serde_core | 1.0.229 | cargo | transitiva | x | x | MIT OR Apache-2.0 | CONFERE | BAIXO |
| serde_json | 1.0.154 | cargo | direta | x | x | MIT OR Apache-2.0 | CONFERE | BAIXO |
| serde_spanned | 1.1.2 | cargo | transitiva | x | x | MIT OR Apache-2.0 | CONFERE | BAIXO |
| serde_with | 3.24.0 | cargo | transitiva | x | x | MIT OR Apache-2.0 | CONFERE | BAIXO |
| serial2 | 0.2.39 | cargo | transitiva | x | x | BSD-2-Clause OR Apache-2.0 | CONFERE | BAIXO |
| serialize-to-javascript | 0.1.2 | cargo | transitiva | x | x | MIT OR Apache-2.0 | CONFERE | BAIXO |
| servo_arc | 0.4.3 | cargo | transitiva | x | x | MIT OR Apache-2.0 | CONFERE | BAIXO |
| shared_child | 1.1.2 | cargo | transitiva | x | x | MIT | CONFERE | BAIXO |
| shared_library | 0.1.9 | cargo | transitiva | x |  | Apache-2.0/MIT | CONFERE | BAIXO |
| shell-words | 1.1.1 | cargo | transitiva | x | x | MIT/Apache-2.0 | CONFERE | BAIXO |
| signal-hook-registry | 1.4.8 | cargo | transitiva |  | x | MIT OR Apache-2.0 | CONFERE | BAIXO |
| simdutf8 | 0.1.5 | cargo | transitiva | x | x | MIT OR Apache-2.0 | CONFERE | BAIXO |
| siphasher | 1.0.4 | cargo | transitiva | x | x | MIT OR Apache-2.0 | CONFERE | BAIXO |
| slab | 0.4.12 | cargo | transitiva |  | x | MIT | CONFERE | BAIXO |
| smallvec | 1.16.4 | cargo | transitiva | x | x | MIT OR Apache-2.0 | CONFERE | BAIXO |
| socket2 | 0.6.5 | cargo | transitiva | x | x | MIT OR Apache-2.0 | CONFERE | BAIXO |
| softbuffer | 0.4.8 | cargo | transitiva | x |  | MIT OR Apache-2.0 | CONFERE | BAIXO |
| soup3 | 0.5.0 | cargo | transitiva |  | x | MIT | CONFERE | BAIXO |
| soup3-sys | 0.5.0 | cargo | transitiva |  | x | MIT | CONFERE | BAIXO |
| stable_deref_trait | 1.2.1 | cargo | transitiva | x | x | MIT OR Apache-2.0 | CONFERE | BAIXO |
| string_cache | 0.9.0 | cargo | transitiva | x | x | MIT OR Apache-2.0 | CONFERE | BAIXO |
| tao | 0.37.2 | cargo | transitiva | x | x | Apache-2.0 | CONFERE | BAIXO |
| tauri | 2.12.7 | cargo | direta | x | x | Apache-2.0 OR MIT | CONFERE | BAIXO |
| tauri-plugin-dialog | 2.8.4 | cargo | direta | x | x | Apache-2.0 OR MIT | CONFERE | BAIXO |
| tauri-plugin-fs | 2.7.1 | cargo | transitiva | x | x | Apache-2.0 OR MIT | CONFERE | BAIXO |
| tauri-plugin-shell | 2.6.0 | cargo | direta | x | x | Apache-2.0 OR MIT | CONFERE | BAIXO |
| tauri-plugin-single-instance | 2.5.5 | cargo | direta | x | x | Apache-2.0 OR MIT | CONFERE | BAIXO |
| tauri-runtime | 2.12.4 | cargo | transitiva | x | x | Apache-2.0 OR MIT | CONFERE | BAIXO |
| tauri-runtime-wry | 2.12.5 | cargo | transitiva | x | x | Apache-2.0 OR MIT | CONFERE | BAIXO |
| tauri-utils | 2.10.4 | cargo | transitiva | x | x | Apache-2.0 OR MIT | CONFERE | BAIXO |
| tendril | 0.5.1 | cargo | transitiva | x | x | MIT OR Apache-2.0 | CONFERE | BAIXO |
| thiserror | 1.0.69 | cargo | transitiva | x | x | MIT OR Apache-2.0 | CONFERE | BAIXO |
| thiserror | 2.0.21 | cargo | transitiva | x | x | MIT OR Apache-2.0 | CONFERE | BAIXO |
| time | 0.3.55 | cargo | transitiva | x | x | MIT OR Apache-2.0 | CONFERE | BAIXO |
| time-core | 0.1.9 | cargo | transitiva | x | x | MIT OR Apache-2.0 | CONFERE | BAIXO |
| tinystr | 0.8.5 | cargo | transitiva | x | x | Unicode-3.0 | CONFERE | BAIXO |
| tinyvec | 1.13.3 | cargo | transitiva | x | x | Zlib OR Apache-2.0 OR MIT | CONFERE | BAIXO |
| tokio | 1.54.1 | cargo | direta | x | x | MIT | CONFERE | BAIXO |
| toml | 1.1.8+spec-1.1.0 | cargo | transitiva | x | x | MIT OR Apache-2.0 | CONFERE | BAIXO |
| toml_datetime | 1.1.2+spec-1.1.0 | cargo | transitiva | x | x | MIT OR Apache-2.0 | CONFERE | BAIXO |
| toml_parser | 1.1.5+spec-1.1.0 | cargo | transitiva | x | x | MIT OR Apache-2.0 | CONFERE | BAIXO |
| toml_writer | 1.1.3+spec-1.1.0 | cargo | transitiva | x | x | MIT OR Apache-2.0 | CONFERE | BAIXO |
| tracing | 0.1.44 | cargo | transitiva | x | x | MIT | CONFERE | BAIXO |
| tracing-core | 0.1.36 | cargo | transitiva | x | x | MIT | CONFERE | BAIXO |
| tray-icon | 0.25.1 | cargo | transitiva | x | x | MIT OR Apache-2.0 | CONFERE | BAIXO |
| typeid | 1.0.3 | cargo | transitiva | x | x | MIT OR Apache-2.0 | CONFERE | BAIXO |
| unicode-ident | 1.0.27 | cargo | transitiva | x | x | (MIT OR Apache-2.0) AND Unicode-3.0 | CONFERE | BAIXO |
| unicode-segmentation | 1.13.3 | cargo | transitiva | x |  | MIT OR Apache-2.0 | CONFERE | BAIXO |
| url | 2.5.8 | cargo | transitiva | x | x | MIT OR Apache-2.0 | CONFERE | BAIXO |
| urlpattern | 0.6.0 | cargo | transitiva | x | x | MIT | CONFERE | BAIXO |
| utf8_iter | 1.0.4 | cargo | transitiva | x | x | Apache-2.0 OR MIT | CONFERE | BAIXO |
| uuid | 1.29.0 | cargo | transitiva | x | x | Apache-2.0 OR MIT | CONFERE | BAIXO |
| walkdir | 2.5.0 | cargo | transitiva | x | x | Unlicense/MIT | CONFERE | BAIXO |
| web-time | 1.1.0 | cargo | transitiva | x | x | MIT OR Apache-2.0 | CONFERE | BAIXO |
| web_atoms | 0.2.6 | cargo | transitiva | x | x | MIT OR Apache-2.0 | CONFERE | BAIXO |
| webkit2gtk | 2.0.2 | cargo | transitiva |  | x | MIT | CONFERE | BAIXO |
| webkit2gtk-sys | 2.0.2 | cargo | transitiva |  | x | MIT | CONFERE | BAIXO |
| webview2-com | 0.39.1 | cargo | transitiva | x |  | MIT | SEM ARQUIVO | BAIXO |
| webview2-com-sys | 0.39.1 | cargo | transitiva | x |  | MIT | SEM ARQUIVO | BAIXO |
| winapi | 0.3.9 | cargo | transitiva | x |  | MIT/Apache-2.0 | CONFERE | BAIXO |
| winapi-util | 0.1.11 | cargo | transitiva | x |  | Unlicense OR MIT | CONFERE | BAIXO |
| window-vibrancy | 0.8.1 | cargo | transitiva | x |  | Apache-2.0 OR MIT | CONFERE | BAIXO |
| windows | 0.62.2 | cargo | transitiva | x |  | MIT OR Apache-2.0 | CONFERE | BAIXO |
| windows-collections | 0.3.2 | cargo | transitiva | x |  | MIT OR Apache-2.0 | CONFERE | BAIXO |
| windows-core | 0.62.2 | cargo | transitiva | x |  | MIT OR Apache-2.0 | CONFERE | BAIXO |
| windows-future | 0.3.2 | cargo | transitiva | x |  | MIT OR Apache-2.0 | CONFERE | BAIXO |
| windows-link | 0.2.1 | cargo | transitiva | x |  | MIT OR Apache-2.0 | CONFERE | BAIXO |
| windows-numerics | 0.3.1 | cargo | transitiva | x |  | MIT OR Apache-2.0 | CONFERE | BAIXO |
| windows-result | 0.4.1 | cargo | transitiva | x |  | MIT OR Apache-2.0 | CONFERE | BAIXO |
| windows-strings | 0.5.1 | cargo | transitiva | x |  | MIT OR Apache-2.0 | CONFERE | BAIXO |
| windows-sys | 0.60.2 | cargo | transitiva | x |  | MIT OR Apache-2.0 | CONFERE | BAIXO |
| windows-sys | 0.61.2 | cargo | transitiva | x |  | MIT OR Apache-2.0 | CONFERE | BAIXO |
| windows-targets | 0.53.5 | cargo | transitiva | x |  | MIT OR Apache-2.0 | CONFERE | BAIXO |
| windows-threading | 0.2.1 | cargo | transitiva | x |  | MIT OR Apache-2.0 | CONFERE | BAIXO |
| windows-version | 0.1.7 | cargo | transitiva | x |  | MIT OR Apache-2.0 | CONFERE | BAIXO |
| windows_x86_64_msvc | 0.53.1 | cargo | transitiva | x |  | MIT OR Apache-2.0 | CONFERE | BAIXO |
| winnow | 1.0.4 | cargo | transitiva | x | x | MIT | CONFERE | BAIXO |
| winreg | 0.10.1 | cargo | transitiva | x |  | MIT | CONFERE | BAIXO |
| writeable | 0.6.4 | cargo | transitiva | x | x | Unicode-3.0 | CONFERE | BAIXO |
| wry | 0.57.1 | cargo | transitiva | x | x | Apache-2.0 OR MIT | CONFERE | BAIXO |
| x11 | 2.21.0 | cargo | transitiva |  | x | MIT | CONFERE | BAIXO |
| x11-dl | 2.21.0 | cargo | transitiva |  | x | MIT | CONFERE | BAIXO |
| yoke | 0.8.4 | cargo | transitiva | x | x | Unicode-3.0 | CONFERE | BAIXO |
| zbus | 5.19.0 | cargo | transitiva |  | x | MIT | CONFERE | BAIXO |
| zbus_names | 4.3.5 | cargo | transitiva |  | x | MIT | CONFERE | BAIXO |
| zcheapstr | 1.1.0 | cargo | transitiva |  | x | MIT | CONFERE | BAIXO |
| zerofrom | 0.1.8 | cargo | transitiva | x | x | Unicode-3.0 | CONFERE | BAIXO |
| zerotrie | 0.2.5 | cargo | transitiva | x | x | Unicode-3.0 | CONFERE | BAIXO |
| zerovec | 0.11.9 | cargo | transitiva | x | x | Unicode-3.0 | CONFERE | BAIXO |
| zmij | 1.0.23 | cargo | transitiva | x | x | MIT | CONFERE | BAIXO |
| zvariant | 5.15.1 | cargo | transitiva |  | x | MIT | CONFERE | BAIXO |
| zvariant_utils | 4.2.0 | cargo | transitiva |  | x | MIT | CONFERE | BAIXO |
| @babel/code-frame | 7.29.7 | npm | transitiva | x | x | MIT | CONFERE | BAIXO |
| @babel/generator | 7.29.8 | npm | transitiva | x | x | MIT | CONFERE | BAIXO |
| @babel/helper-globals | 7.29.7 | npm | transitiva | x | x | MIT | CONFERE | BAIXO |
| @babel/helper-module-imports | 7.29.7 | npm | transitiva | x | x | MIT | CONFERE | BAIXO |
| @babel/helper-string-parser | 7.29.11 | npm | transitiva | x | x | MIT | CONFERE | BAIXO |
| @babel/helper-validator-identifier | 7.29.7 | npm | transitiva | x | x | MIT | CONFERE | BAIXO |
| @babel/parser | 7.29.11 | npm | transitiva | x | x | MIT | CONFERE | BAIXO |
| @babel/runtime | 7.29.10 | npm | transitiva | x | x | MIT | CONFERE | BAIXO |
| @babel/runtime | 8.0.8 | npm | transitiva | x | x | MIT | CONFERE | BAIXO |
| @babel/template | 7.29.7 | npm | transitiva | x | x | MIT | CONFERE | BAIXO |
| @babel/traverse | 7.29.10 | npm | transitiva | x | x | MIT | CONFERE | BAIXO |
| @babel/types | 7.29.11 | npm | transitiva | x | x | MIT | CONFERE | BAIXO |
| @base-ui/utils | 0.5.0 | npm | transitiva | x | x | MIT | CONFERE | BAIXO |
| @emotion/babel-plugin | 11.13.5 | npm | transitiva | x | x | MIT | CONFERE | BAIXO |
| @emotion/cache | 11.14.0 | npm | transitiva | x | x | MIT | CONFERE | BAIXO |
| @emotion/hash | 0.9.2 | npm | transitiva | x | x | MIT | CONFERE | BAIXO |
| @emotion/is-prop-valid | 1.4.0 | npm | transitiva | x | x | MIT | CONFERE | BAIXO |
| @emotion/memoize | 0.9.0 | npm | transitiva | x | x | MIT | CONFERE | BAIXO |
| @emotion/react | 11.14.0 | npm | direta | x | x | MIT | CONFERE | BAIXO |
| @emotion/serialize | 1.3.3 | npm | transitiva | x | x | MIT | CONFERE | BAIXO |
| @emotion/sheet | 1.4.0 | npm | transitiva | x | x | MIT | CONFERE | BAIXO |
| @emotion/styled | 11.14.1 | npm | direta | x | x | MIT | CONFERE | BAIXO |
| @emotion/unitless | 0.10.0 | npm | transitiva | x | x | MIT | CONFERE | BAIXO |
| @emotion/use-insertion-effect-with-fallbacks | 1.2.0 | npm | transitiva | x | x | MIT | CONFERE | BAIXO |
| @emotion/utils | 1.4.2 | npm | transitiva | x | x | MIT | CONFERE | BAIXO |
| @emotion/weak-memoize | 0.4.0 | npm | transitiva | x | x | MIT | CONFERE | BAIXO |
| @floating-ui/utils | 0.2.12 | npm | transitiva | x | x | MIT | CONFERE | BAIXO |
| @hookform/resolvers | 7.0.0 | npm | direta | x | x | MIT | CONFERE | BAIXO |
| @jridgewell/gen-mapping | 0.3.13 | npm | transitiva | x | x | MIT | CONFERE | BAIXO |
| @jridgewell/resolve-uri | 3.1.2 | npm | transitiva | x | x | MIT | CONFERE | BAIXO |
| @jridgewell/sourcemap-codec | 1.6.0 | npm | transitiva | x | x | MIT | CONFERE | BAIXO |
| @jridgewell/trace-mapping | 0.3.31 | npm | transitiva | x | x | MIT | CONFERE | BAIXO |
| @mariliz/ui-kit | 0.6.0 | npm | direta | x | x | MIT | SEM ARQUIVO | BAIXO |
| @mui/core-downloads-tracker | 9.5.0 | npm | transitiva | x | x | MIT | CONFERE | BAIXO |
| @mui/material | 9.5.0 | npm | direta | x | x | MIT | CONFERE | BAIXO |
| @mui/private-theming | 9.5.0 | npm | transitiva | x | x | MIT | CONFERE | BAIXO |
| @mui/styled-engine | 9.5.0 | npm | transitiva | x | x | MIT | CONFERE | BAIXO |
| @mui/system | 9.5.0 | npm | transitiva | x | x | MIT | CONFERE | BAIXO |
| @mui/types | 9.5.0 | npm | transitiva | x | x | MIT | CONFERE | BAIXO |
| @mui/utils | 9.5.0 | npm | transitiva | x | x | MIT | CONFERE | BAIXO |
| @mui/x-data-grid | 9.16.0 | npm | transitiva | x | x | MIT | CONFERE | BAIXO |
| @mui/x-internals | 9.16.0 | npm | transitiva | x | x | MIT | CONFERE | BAIXO |
| @mui/x-virtualizer | 0.7.4 | npm | transitiva | x | x | MIT | CONFERE | BAIXO |
| @popperjs/core | 2.11.8 | npm | transitiva | x | x | MIT | CONFERE | BAIXO |
| @remix-run/route-pattern | 0.22.1 | npm | transitiva | x | x | MIT | CONFERE | BAIXO |
| @standard-schema/utils | 0.3.0 | npm | transitiva | x | x | MIT | CONFERE | BAIXO |
| @tauri-apps/api | 2.12.5 | npm | direta | x | x | Apache-2.0 OR MIT | CONFERE | BAIXO |
| @types/parse-json | 4.0.2 | npm | transitiva | x | x | MIT | CONFERE | BAIXO |
| @types/prop-types | 15.7.15 | npm | transitiva | x | x | MIT | CONFERE | BAIXO |
| @types/react | 19.3.0 | npm | direta | x | x | MIT | CONFERE | BAIXO |
| @types/react-transition-group | 4.4.12 | npm | transitiva | x | x | MIT | CONFERE | BAIXO |
| @xterm/addon-fit | 0.11.0 | npm | direta | x | x | MIT | CONFERE | BAIXO |
| @xterm/xterm | 6.0.0 | npm | direta | x | x | MIT | CONFERE | BAIXO |
| babel-plugin-macros | 3.1.0 | npm | transitiva | x | x | MIT | CONFERE | BAIXO |
| callsites | 3.1.0 | npm | transitiva | x | x | MIT | CONFERE | BAIXO |
| classnames | 2.5.1 | npm | transitiva | x | x | MIT | CONFERE | BAIXO |
| clsx | 2.1.1 | npm | direta | x | x | MIT | CONFERE | BAIXO |
| convert-source-map | 1.9.0 | npm | transitiva | x | x | MIT | CONFERE | BAIXO |
| cookie-es | 3.1.1 | npm | transitiva | x | x | MIT | CONFERE | BAIXO |
| cosmiconfig | 7.1.0 | npm | transitiva | x | x | MIT | CONFERE | BAIXO |
| csstype | 3.2.3 | npm | transitiva | x | x | MIT | CONFERE | BAIXO |
| debug | 4.4.3 | npm | transitiva | x | x | MIT | CONFERE | BAIXO |
| dom-helpers | 5.2.1 | npm | transitiva | x | x | MIT | CONFERE | BAIXO |
| error-ex | 1.3.4 | npm | transitiva | x | x | MIT | CONFERE | BAIXO |
| es-errors | 1.3.0 | npm | transitiva | x | x | MIT | CONFERE | BAIXO |
| escape-string-regexp | 4.0.0 | npm | transitiva | x | x | MIT | CONFERE | BAIXO |
| find-root | 1.1.0 | npm | transitiva | x | x | MIT | CONFERE | BAIXO |
| function-bind | 1.1.2 | npm | transitiva | x | x | MIT | CONFERE | BAIXO |
| hasown | 2.0.4 | npm | transitiva | x | x | MIT | CONFERE | BAIXO |
| hoist-non-react-statics | 3.3.2 | npm | transitiva | x | x | BSD-3-Clause | TEXTO NÃO RECONHECIDO | BAIXO |
| import-fresh | 3.3.1 | npm | transitiva | x | x | MIT | CONFERE | BAIXO |
| is-arrayish | 0.2.1 | npm | transitiva | x | x | MIT | CONFERE | BAIXO |
| is-core-module | 2.17.0 | npm | transitiva | x | x | MIT | CONFERE | BAIXO |
| js-tokens | 4.0.0 | npm | transitiva | x | x | MIT | CONFERE | BAIXO |
| jsesc | 3.1.0 | npm | transitiva | x | x | MIT | CONFERE | BAIXO |
| json-parse-even-better-errors | 2.3.1 | npm | transitiva | x | x | MIT | CONFERE | BAIXO |
| lines-and-columns | 1.2.4 | npm | transitiva | x | x | MIT | CONFERE | BAIXO |
| loose-envify | 1.4.0 | npm | transitiva | x | x | MIT | CONFERE | BAIXO |
| ms | 2.1.3 | npm | transitiva | x | x | MIT | CONFERE | BAIXO |
| object-assign | 4.1.1 | npm | transitiva | x | x | MIT | CONFERE | BAIXO |
| parent-module | 1.0.1 | npm | transitiva | x | x | MIT | CONFERE | BAIXO |
| parse-json | 5.2.0 | npm | transitiva | x | x | MIT | CONFERE | BAIXO |
| path-parse | 1.0.7 | npm | transitiva | x | x | MIT | CONFERE | BAIXO |
| path-type | 4.0.0 | npm | transitiva | x | x | MIT | CONFERE | BAIXO |
| picocolors | 1.1.1 | npm | transitiva | x | x | ISC | CONFERE | BAIXO |
| prop-types | 15.8.1 | npm | transitiva | x | x | MIT | CONFERE | BAIXO |
| react | 19.3.1 | npm | direta | x | x | MIT | CONFERE | BAIXO |
| react-code-input | 3.10.1 | npm | transitiva | x | x | MIT | CONFERE | BAIXO |
| react-dom | 19.3.1 | npm | direta | x | x | MIT | CONFERE | BAIXO |
| react-hook-form | 7.91.0 | npm | direta | x | x | MIT | CONFERE | BAIXO |
| react-is | 16.13.1 | npm | transitiva | x | x | MIT | CONFERE | BAIXO |
| react-is | 19.3.1 | npm | transitiva | x | x | MIT | CONFERE | BAIXO |
| react-router | 8.5.0 | npm | direta | x | x | MIT | CONFERE | BAIXO |
| react-transition-group | 4.4.5 | npm | transitiva | x | x | BSD-3-Clause | CONFERE | BAIXO |
| reselect | 5.3.0 | npm | transitiva | x | x | MIT | CONFERE | BAIXO |
| resolve | 1.22.13 | npm | transitiva | x | x | MIT | CONFERE | BAIXO |
| resolve-from | 4.0.0 | npm | transitiva | x | x | MIT | CONFERE | BAIXO |
| scheduler | 0.28.0 | npm | transitiva | x | x | MIT | CONFERE | BAIXO |
| source-map | 0.5.7 | npm | transitiva | x | x | BSD-3-Clause | CONFERE | BAIXO |
| stylis | 4.2.0 | npm | transitiva | x | x | MIT | CONFERE | BAIXO |
| supports-preserve-symlinks-flag | 1.0.0 | npm | transitiva | x | x | MIT | CONFERE | BAIXO |
| tslib | 2.8.1 | npm | transitiva | x | x | 0BSD | CONFERE | BAIXO |
| use-sync-external-store | 1.7.0 | npm | transitiva | x | x | MIT | CONFERE | BAIXO |
| yaml | 1.10.3 | npm | transitiva | x | x | ISC | CONFERE | BAIXO |
| zod | 4.6.7 | npm | direta | x | x | MIT | CONFERE | BAIXO |
| AWSSDK.CloudFormation | 4.1.1 | nuget | direta | x | x | Apache-2.0 | SÓ EXPRESSÃO | BAIXO |
| AWSSDK.CloudWatch | 4.1.1 | nuget | direta | x | x | Apache-2.0 | SÓ EXPRESSÃO | BAIXO |
| AWSSDK.Core | 4.1.0.1 | nuget | transitiva | x | x | Apache-2.0 | SÓ EXPRESSÃO | BAIXO |
| AWSSDK.EC2 | 4.1.2 | nuget | direta | x | x | Apache-2.0 | SÓ EXPRESSÃO | BAIXO |
| AWSSDK.ECR | 4.1.0 | nuget | direta | x | x | Apache-2.0 | SÓ EXPRESSÃO | BAIXO |
| AWSSDK.ECS | 4.1.0 | nuget | direta | x | x | Apache-2.0 | SÓ EXPRESSÃO | BAIXO |
| AWSSDK.Extensions.CborProtocol | 4.1.0 | nuget | transitiva | x | x | Apache-2.0 | SÓ EXPRESSÃO | BAIXO |
| AWSSDK.Extensions.Numerics | 4.1.0 | nuget | transitiva | x | x | Apache-2.0 | SÓ EXPRESSÃO | BAIXO |
| AWSSDK.Lambda | 4.1.1 | nuget | direta | x | x | Apache-2.0 | SÓ EXPRESSÃO | BAIXO |
| AWSSDK.Pricing | 4.1.0 | nuget | direta | x | x | Apache-2.0 | SÓ EXPRESSÃO | BAIXO |
| AWSSDK.RDS | 4.1.0 | nuget | direta | x | x | Apache-2.0 | SÓ EXPRESSÃO | BAIXO |
| AWSSDK.S3 | 4.1.0 | nuget | direta | x | x | Apache-2.0 | SÓ EXPRESSÃO | BAIXO |
| AWSSDK.SecurityToken | 4.1.0 | nuget | direta | x | x | Apache-2.0 | SÓ EXPRESSÃO | BAIXO |
| AWSSDK.Signin | 4.1.0 | nuget | direta | x | x | Apache-2.0 | SÓ EXPRESSÃO | BAIXO |
| AWSSDK.SSO | 4.1.0 | nuget | direta | x | x | Apache-2.0 | SÓ EXPRESSÃO | BAIXO |
| AWSSDK.SSOOIDC | 4.1.0 | nuget | direta | x | x | Apache-2.0 | SÓ EXPRESSÃO | BAIXO |
| Azure.Core | 1.53.0 | nuget | transitiva | x | x | MIT | SÓ EXPRESSÃO | BAIXO |
| Azure.Identity | 1.21.0 | nuget | direta | x | x | MIT | SÓ EXPRESSÃO | BAIXO |
| Azure.ResourceManager | 1.14.0 | nuget | direta | x | x | MIT | SÓ EXPRESSÃO | BAIXO |
| Google.Apis | 1.78.0 | nuget | transitiva | x | x | Apache-2.0 | CONFERE | BAIXO |
| Google.Apis.Auth | 1.78.0 | nuget | direta | x | x | Apache-2.0 | CONFERE | BAIXO |
| Google.Apis.CloudResourceManager.v3 | 1.78.0.4328 | nuget | direta | x | x | Apache-2.0 | CONFERE | BAIXO |
| Google.Apis.Compute.v1 | 1.78.0.4327 | nuget | direta | x | x | Apache-2.0 | CONFERE | BAIXO |
| Google.Apis.Core | 1.78.0 | nuget | transitiva | x | x | Apache-2.0 | CONFERE | BAIXO |
| Google.Apis.ServiceUsage.v1 | 1.78.0.4281 | nuget | direta | x | x | Apache-2.0 | CONFERE | BAIXO |
| Google.Apis.Storage.v1 | 1.78.0.4299 | nuget | direta | x | x | Apache-2.0 | CONFERE | BAIXO |
| Microsoft .NET Runtime para linux-x64 (self-contained) | 10.0.x | nuget | direta |  | x | MIT | CONFERIDO MANUALMENTE | MÉDIO |
| Microsoft .NET Runtime para win-x64 (self-contained) | 10.0.x | nuget | direta | x |  | MIT AND LicenseRef-DotNet-Library-License | CONFERIDO MANUALMENTE | MÉDIO |
| Microsoft.Bcl.AsyncInterfaces | 10.0.3 | nuget | transitiva | x | x | MIT | SÓ EXPRESSÃO | BAIXO |
| Microsoft.Extensions.AI.Abstractions | 10.11.0 | nuget | transitiva | x | x | MIT | SÓ EXPRESSÃO | BAIXO |
| Microsoft.Extensions.Configuration.Abstractions | 10.0.3 | nuget | transitiva | x | x | MIT | SÓ EXPRESSÃO | BAIXO |
| Microsoft.Extensions.DependencyInjection.Abstractions | 10.0.14 | nuget | transitiva | x | x | MIT | SÓ EXPRESSÃO | BAIXO |
| Microsoft.Extensions.Diagnostics.Abstractions | 10.0.3 | nuget | transitiva | x | x | MIT | SÓ EXPRESSÃO | BAIXO |
| Microsoft.Extensions.FileProviders.Abstractions | 10.0.3 | nuget | transitiva | x | x | MIT | SÓ EXPRESSÃO | BAIXO |
| Microsoft.Extensions.Hosting.Abstractions | 10.0.3 | nuget | transitiva | x | x | MIT | SÓ EXPRESSÃO | BAIXO |
| Microsoft.Extensions.Logging.Abstractions | 10.0.14 | nuget | transitiva | x | x | MIT | SÓ EXPRESSÃO | BAIXO |
| Microsoft.Extensions.Options | 10.0.3 | nuget | transitiva | x | x | MIT | SÓ EXPRESSÃO | BAIXO |
| Microsoft.Extensions.Primitives | 10.0.3 | nuget | transitiva | x | x | MIT | SÓ EXPRESSÃO | BAIXO |
| Microsoft.Identity.Client | 4.83.1 | nuget | transitiva | x | x | MIT | SÓ EXPRESSÃO | BAIXO |
| Microsoft.Identity.Client.Extensions.Msal | 4.83.1 | nuget | transitiva | x | x | MIT | SÓ EXPRESSÃO | BAIXO |
| Microsoft.IdentityModel.Abstractions | 8.14.0 | nuget | transitiva | x | x | MIT | SÓ EXPRESSÃO | BAIXO |
| Microsoft.ML.Tokenizers | 3.0.0 | nuget | direta | x | x | MIT | CONFERE | BAIXO |
| Microsoft.ML.Tokenizers.Data.O200kBase | 3.0.0 | nuget | direta | x | x | MIT | CONFERE | BAIXO |
| ModelContextProtocol.Core | 2.4.0 | nuget | direta | x | x | Apache-2.0 | SÓ EXPRESSÃO | BAIXO |
| Newtonsoft.Json | 13.0.4 | nuget | transitiva | x | x | MIT | CONFERE | BAIXO |
| System.ClientModel | 1.10.0 | nuget | transitiva | x | x | MIT | SÓ EXPRESSÃO | BAIXO |
| System.CodeDom | 7.0.0 | nuget | transitiva | x | x | MIT | CONFERE | BAIXO |
| System.Formats.Cbor | 11.0.0 | nuget | transitiva | x | x | MIT | SÓ EXPRESSÃO | BAIXO |
| System.IO.Hashing | 11.0.0 | nuget | transitiva | x | x | MIT | SÓ EXPRESSÃO | BAIXO |
| System.Management | 7.0.2 | nuget | transitiva | x | x | MIT | CONFERE | BAIXO |
| System.Memory.Data | 10.0.3 | nuget | transitiva | x | x | MIT | SÓ EXPRESSÃO | BAIXO |
| System.Security.Cryptography.ProtectedData | 9.0.11 | nuget | direta | x | x | MIT | CONFERE | BAIXO |
| YamlDotNet | 18.1.0 | nuget | direta | x | x | MIT | SÓ EXPRESSÃO | BAIXO |
| aiosqlite | 0.22.1 | pypi | transitiva | x | x | MIT | CONFERE | BAIXO |
| annotated-types | 0.8.0 | pypi | transitiva | x | x | MIT | CONFERE | BAIXO |
| anyio | 4.16.0 | pypi | transitiva | x | x | MIT | CONFERE | BAIXO |
| certifi | 2026.10.23 | pypi | transitiva | x | x | MPL-2.0 | CONFERE | MÉDIO |
| charset-normalizer | 3.5.2 | pypi | transitiva | x | x | MIT | CONFERE | BAIXO |
| distro | 1.9.0 | pypi | transitiva | x | x | Apache License, Version 2.0 | CONFERE | BAIXO |
| h11 | 0.16.0 | pypi | transitiva | x | x | MIT | CONFERE | BAIXO |
| httpcore | 1.0.9 | pypi | transitiva | x | x | BSD-3-Clause | CONFERE | BAIXO |
| httpcore2 | 2.16.0 | pypi | transitiva | x | x | BSD-3-Clause | CONFERE | BAIXO |
| httpx | 0.28.1 | pypi | transitiva | x | x | BSD-3-Clause | CONFERE | BAIXO |
| httpx2 | 2.16.0 | pypi | transitiva | x | x | BSD-3-Clause | CONFERE | BAIXO |
| idna | 3.20 | pypi | transitiva | x | x | BSD-3-Clause | CONFERE | BAIXO |
| jsonpatch | 1.41 | pypi | transitiva | x | x | BSD-3-Clause | CONFERE | BAIXO |
| jsonpointer | 3.8 | pypi | transitiva | x | x | Modified BSD License | CONFERE | BAIXO |
| langchain-core | 1.7.4 | pypi | transitiva | x | x | MIT | CONFERE | BAIXO |
| langchain-protocol | 0.0.20 | pypi | transitiva | x | x | MIT | CONFERE | BAIXO |
| langgraph | 1.2.19 | pypi | direta | x | x | MIT | CONFERE | BAIXO |
| langgraph-checkpoint | 4.3.1 | pypi | transitiva | x | x | MIT | CONFERE | BAIXO |
| langgraph-checkpoint-sqlite | 3.1.3 | pypi | direta | x | x | MIT | CONFERE | BAIXO |
| langgraph-prebuilt | 1.1.1 | pypi | transitiva | x | x | MIT | CONFERE | BAIXO |
| langgraph-sdk | 0.4.11 | pypi | transitiva | x | x | MIT | CONFERE | BAIXO |
| langsmith | 0.14.17 | pypi | transitiva | x | x | MIT | CONFERE | BAIXO |
| orjson | 3.13.0 | pypi | transitiva | x | x | MPL-2.0 AND (Apache-2.0 OR MIT) | CONFERE | MÉDIO |
| ormsgpack | 1.13.0 | pypi | transitiva | x | x | Apache-2.0 OR MIT | CONFERE | BAIXO |
| packaging | 26.3 | pypi | transitiva | x | x | Apache-2.0 OR BSD-2-Clause | CONFERE | BAIXO |
| pydantic | 2.14.2 | pypi | transitiva | x | x | MIT | CONFERE | BAIXO |
| pydantic_core | 2.50.2 | pypi | transitiva | x | x | MIT | CONFERE | BAIXO |
| PyYAML | 6.0.3 | pypi | transitiva | x | x | MIT | CONFERE | BAIXO |
| requests | 2.34.2 | pypi | transitiva | x | x | Apache-2.0 | CONFERE | BAIXO |
| requests-toolbelt | 1.0.0 | pypi | transitiva | x | x | Apache 2.0 | CONFERE | BAIXO |
| sniffio | 1.3.1 | pypi | transitiva | x | x | MIT OR Apache-2.0 | CONFERE | BAIXO |
| sqlite-vec | 0.1.9 | pypi | transitiva | x | x | MIT License, Apache License, Version 2.0 | SEM ARQUIVO | BAIXO |
| tenacity | 9.2.1 | pypi | transitiva | x | x | Apache-2.0 | CONFERE | BAIXO |
| truststore | 0.10.5 | pypi | transitiva | x | x | MIT | CONFERE | BAIXO |
| typing-inspection | 0.4.4 | pypi | transitiva | x | x | MIT | CONFERE | BAIXO |
| typing_extensions | 4.16.0 | pypi | transitiva | x | x | PSF-2.0 | CONFERE | BAIXO |
| urllib3 | 2.8.0 | pypi | transitiva | x | x | MIT | CONFERE | BAIXO |
| uuid_utils | 0.19.0 | pypi | transitiva | x | x | BSD-3-Clause | CONFERE | BAIXO |
| websockets | 17.4 | pypi | transitiva | x | x | BSD-3-Clause | CONFERE | BAIXO |
| xxhash | 4.0.2 | pypi | transitiva | x | x | BSD-2-Clause | CONFERE | BAIXO |
| zstandard | 0.25.0 | pypi | transitiva | x | x | BSD-3-Clause | CONFERE | BAIXO |
