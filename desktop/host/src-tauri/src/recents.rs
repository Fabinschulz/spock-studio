//! Projetos recentes. A lista mora no host, no diretório de dados do aplicativo, e só recebe pastas que a pessoa
//! escolheu pelo diálogo nativo e que o motor autorizou. A UI nunca vê o caminho completo nem consegue pedir um
//! caminho: ela recebe um id opaco, o nome e o caminho abreviado, e reabre um projeto pelo id, por um comando do host.

use std::cmp::Reverse;
use std::fs;
use std::io;
use std::path::Path;

use serde::{Deserialize, Serialize};

pub const MAX_RECENTS: usize = 6;
pub const FILE_NAME: &str = "recent-projects.json";

/// O que fica no disco. `path` nunca sai do host.
#[derive(Debug, Clone, Serialize, Deserialize, PartialEq)]
#[serde(rename_all = "camelCase")]
pub struct StoredProject {
    pub id: String,
    pub path: String,
    pub display_name: String,
    pub display_path: String,
    pub last_opened_ms: u64,
}

/// O que a UI recebe: sem o caminho completo.
#[derive(Debug, Clone, Serialize, PartialEq)]
#[serde(rename_all = "camelCase")]
pub struct RecentProject {
    pub id: String,
    pub display_name: String,
    pub display_path: String,
    pub last_opened_ms: u64,
    /// Falso quando a pasta não existe mais ou não está acessível.
    pub available: bool,
}

/// Id opaco e estável para o mesmo caminho. No Windows e no macOS o caminho é comparado sem diferenciar maiúsculas.
/// Usa FNV-1a de 64 bits: o algoritmo do `DefaultHasher` da std pode mudar entre versões do Rust, e o id fica gravado.
pub fn project_id(path: &str) -> String {
    let normalized = if cfg!(any(windows, target_os = "macos")) {
        path.to_lowercase()
    } else {
        path.to_string()
    };
    format!("r-{:016x}", fnv1a(normalized.as_bytes()))
}

fn fnv1a(bytes: &[u8]) -> u64 {
    const OFFSET: u64 = 0xcbf2_9ce4_8422_2325;
    const PRIME: u64 = 0x0000_0100_0000_01b3;
    bytes.iter().fold(OFFSET, |hash, byte| {
        (hash ^ u64::from(*byte)).wrapping_mul(PRIME)
    })
}

#[derive(Debug, Default)]
pub struct RecentStore {
    entries: Vec<StoredProject>,
}

impl RecentStore {
    /// Lê a lista. Arquivo ausente, ilegível ou corrompido vira lista vazia: perder o histórico não é uma falha.
    pub fn load(file: &Path) -> Self {
        let mut entries = fs::read_to_string(file)
            .ok()
            .and_then(|text| serde_json::from_str::<Vec<StoredProject>>(&text).ok())
            .unwrap_or_default();
        entries.sort_by_key(|entry| Reverse(entry.last_opened_ms));
        // O id é derivado do caminho: recalculado na leitura, uma lista gravada por outra versão continua coerente.
        let mut store = Self {
            entries: Vec::new(),
        };
        for mut entry in entries {
            entry.id = project_id(&entry.path);
            if store.entries.iter().all(|kept| kept.id != entry.id) {
                store.entries.push(entry);
            }
        }
        store.entries.truncate(MAX_RECENTS);
        store
    }

    /// Grava por arquivo temporário e renomeação, para nunca deixar a lista pela metade.
    pub fn save(&self, file: &Path) -> io::Result<()> {
        if let Some(dir) = file.parent() {
            fs::create_dir_all(dir)?;
        }
        let tmp = file.with_extension("json.tmp");
        fs::write(&tmp, serde_json::to_vec_pretty(&self.entries)?)?;
        fs::rename(&tmp, file)
    }

    /// Registra a abertura: o projeto vai para o topo, sem duplicar, e a lista respeita o limite.
    pub fn record(&mut self, path: &str, display_name: &str, display_path: &str, now_ms: u64) {
        let id = project_id(path);
        self.entries.retain(|e| e.id != id);
        self.entries.insert(
            0,
            StoredProject {
                id,
                path: path.to_string(),
                display_name: display_name.to_string(),
                display_path: display_path.to_string(),
                last_opened_ms: now_ms,
            },
        );
        self.entries.truncate(MAX_RECENTS);
    }

    pub fn find(&self, id: &str) -> Option<&StoredProject> {
        self.entries.iter().find(|e| e.id == id)
    }

    /// Visão para a UI. `exists` diz se a pasta ainda está lá; o caminho completo é só entregue a essa função.
    pub fn list(&self, exists: impl Fn(&str) -> bool) -> Vec<RecentProject> {
        self.entries
            .iter()
            .map(|e| RecentProject {
                id: e.id.clone(),
                display_name: e.display_name.clone(),
                display_path: e.display_path.clone(),
                last_opened_ms: e.last_opened_ms,
                available: exists(&e.path),
            })
            .collect()
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn temp_file(name: &str) -> std::path::PathBuf {
        let dir =
            std::env::temp_dir().join(format!("mariliz-recents-{}-{name}", std::process::id()));
        let _ = fs::remove_dir_all(&dir);
        dir.join(FILE_NAME)
    }

    #[test]
    fn o_mais_recente_vem_primeiro_e_o_mesmo_projeto_nao_duplica() {
        let mut store = RecentStore::default();
        store.record("C:\\a", "a", "~\\a", 1);
        store.record("C:\\b", "b", "~\\b", 2);
        store.record("C:\\a", "a", "~\\a", 3);

        let names: Vec<_> = store
            .list(|_| true)
            .into_iter()
            .map(|p| p.display_name)
            .collect();
        assert_eq!(names, ["a", "b"]);
        assert_eq!(store.list(|_| true)[0].last_opened_ms, 3);
    }

    #[test]
    fn a_lista_respeita_o_limite_e_descarta_o_mais_antigo() {
        let mut store = RecentStore::default();
        for i in 0..(MAX_RECENTS + 3) {
            store.record(&format!("C:\\p{i}"), &format!("p{i}"), "~", i as u64);
        }

        let list = store.list(|_| true);
        assert_eq!(list.len(), MAX_RECENTS);
        assert_eq!(list[0].display_name, format!("p{}", MAX_RECENTS + 2));
        assert!(store.find(&project_id("C:\\p0")).is_none());
    }

    #[test]
    fn o_id_e_opaco_estavel_e_nao_revela_o_caminho() {
        let id = project_id("C:\\Users\\fabio\\segredo\\app");

        assert_eq!(id, project_id("C:\\Users\\fabio\\segredo\\app"));
        assert!(id.starts_with("r-"));
        assert!(!id.to_lowercase().contains("fabio") && !id.contains("segredo"));
        assert_ne!(id, project_id("C:\\Users\\fabio\\segredo\\outro"));
    }

    #[test]
    fn a_visao_da_ui_nunca_traz_o_caminho_completo() {
        let mut store = RecentStore::default();
        store.record("C:\\Users\\fabio\\privado\\app", "app", "~\\...\\app", 1);

        let json = serde_json::to_string(&store.list(|_| true)).unwrap();

        assert!(!json.contains("privado") && !json.contains("fabio"));
        assert!(json.contains("\"displayName\":\"app\"") && json.contains("\"available\":true"));
    }

    #[test]
    fn pasta_que_sumiu_aparece_como_indisponivel() {
        let mut store = RecentStore::default();
        store.record("C:\\existe", "existe", "~", 1);
        store.record("C:\\sumiu", "sumiu", "~", 2);

        let list = store.list(|p| p == "C:\\existe");

        assert!(!list[0].available && list[1].available);
    }

    #[test]
    fn grava_e_le_de_volta_sem_perder_nada() {
        let file = temp_file("roundtrip");
        let mut store = RecentStore::default();
        store.record("C:\\a", "a", "~\\a", 10);
        store.record("C:\\b", "b", "~\\b", 20);

        store.save(&file).unwrap();
        let loaded = RecentStore::load(&file);

        assert_eq!(loaded.list(|_| true), store.list(|_| true));
        assert_eq!(loaded.find(&project_id("C:\\a")).unwrap().path, "C:\\a");
        assert!(!file.with_extension("json.tmp").exists());
        let _ = fs::remove_dir_all(file.parent().unwrap());
    }

    #[test]
    fn arquivo_ausente_ou_corrompido_vira_lista_vazia() {
        let missing = temp_file("ausente");
        assert!(RecentStore::load(&missing).list(|_| true).is_empty());

        let corrupt = temp_file("corrompido");
        fs::create_dir_all(corrupt.parent().unwrap()).unwrap();
        fs::write(&corrupt, "{ isto nao e json").unwrap();
        assert!(RecentStore::load(&corrupt).list(|_| true).is_empty());
        let _ = fs::remove_dir_all(corrupt.parent().unwrap());
    }

    #[test]
    fn o_id_nao_depende_da_versao_do_rust() {
        // Vetores conhecidos do FNV-1a de 64 bits.
        assert_eq!(fnv1a(b""), 0xcbf2_9ce4_8422_2325);
        assert_eq!(fnv1a(b"a"), 0xaf63_dc4c_8601_ec8c);
        assert_eq!(project_id("a"), "r-af63dc4c8601ec8c");
    }

    #[test]
    fn lista_gravada_com_ids_antigos_e_corrigida_na_leitura_sem_duplicar() {
        let file = temp_file("ids-antigos");
        fs::create_dir_all(file.parent().unwrap()).unwrap();
        let old = r#"[
            {"id":"r-1111111111111111","path":"C:\\a","displayName":"a","displayPath":"~","lastOpenedMs":2},
            {"id":"r-2222222222222222","path":"C:\\a","displayName":"a","displayPath":"~","lastOpenedMs":1}
        ]"#;
        fs::write(&file, old).unwrap();

        let store = RecentStore::load(&file);

        let list = store.list(|_| true);
        assert_eq!(list.len(), 1);
        assert_eq!(list[0].id, project_id("C:\\a"));
        assert_eq!(list[0].last_opened_ms, 2);
        let _ = fs::remove_dir_all(file.parent().unwrap());
    }

    #[test]
    fn so_se_reabre_o_que_esta_na_lista() {
        let mut store = RecentStore::default();
        store.record("C:\\a", "a", "~", 1);

        assert!(store.find(&project_id("C:\\a")).is_some());
        assert!(store.find(&project_id("C:\\Windows")).is_none());
        assert!(store.find("r-0000000000000000").is_none());
    }
}
