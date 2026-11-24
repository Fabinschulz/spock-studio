//! Só recebe pastas escolhidas pelo diálogo nativo e autorizadas pelo motor. A UI nunca vê o caminho completo nem
//! consegue pedir um: recebe um id opaco, o nome e o caminho abreviado, e reabre um projeto pelo id.

use std::cmp::Reverse;
use std::fs;
use std::io;
use std::path::{Path, PathBuf};
use std::sync::{Arc, Mutex};
use std::time::{SystemTime, UNIX_EPOCH};

use serde::{Deserialize, Serialize};
use serde_json::Value;

use crate::diagnostic_log::Diagnostics;
use crate::lock;
use crate::protocol::{error_code, HostError};

pub const MAX_RECENTS: usize = 6;
pub const FILE_NAME: &str = "recent-projects.json";

/// `path` nunca sai do host.
#[derive(Debug, Clone, Serialize, Deserialize, PartialEq)]
#[serde(rename_all = "camelCase")]
pub struct StoredProject {
    pub id: String,
    pub path: String,
    pub display_name: String,
    pub display_path: String,
    pub last_opened_ms: u64,
}

#[derive(Debug, Clone, Serialize, PartialEq)]
#[serde(rename_all = "camelCase")]
pub struct RecentProject {
    pub id: String,
    pub display_name: String,
    pub display_path: String,
    pub last_opened_ms: u64,
    pub available: bool,
}

/// No Windows e no macOS o caminho é comparado sem diferenciar maiúsculas. FNV-1a de 64 bits porque o algoritmo do
/// `DefaultHasher` da std pode mudar entre versões do Rust, e o id fica gravado.
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

#[derive(Debug, Default, Clone)]
pub struct RecentStore {
    entries: Vec<StoredProject>,
}

impl RecentStore {
    /// Arquivo ausente, ilegível ou corrompido vira lista vazia: perder o histórico não é uma falha.
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

    /// O caminho completo só é entregue a `exists`.
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

/// Só o que a UI pode ver da resposta do motor ao autorizar uma pasta. É o único lugar do host que conhece o formato
/// dessa resposta.
pub struct AuthorizedProject {
    display_name: String,
    display_path: String,
}

impl AuthorizedProject {
    pub fn from_engine(response: &Value) -> Self {
        let text = |field: &str, fallback: &str| {
            response
                .get(field)
                .and_then(Value::as_str)
                .unwrap_or(fallback)
                .to_string()
        };
        Self {
            display_name: text("displayName", "Projeto"),
            display_path: text("displayPath", ""),
        }
    }
}

/// `Clone` divide a mesma lista: conferir uma pasta pode demorar, e quem confere leva um clone para fora da thread do
/// comando.
#[derive(Clone)]
pub struct Recents {
    store: Arc<Mutex<RecentStore>>,
    file: PathBuf,
    diagnostics: Diagnostics,
}

impl Recents {
    pub fn load(file: PathBuf, diagnostics: Diagnostics) -> Self {
        Self {
            store: Arc::new(Mutex::new(RecentStore::load(&file))),
            file,
            diagnostics,
        }
    }

    /// Devolve o id do recente para a UI saber qual item da lista é o projeto atual sem receber o caminho.
    pub fn record(&self, path: &str, project: &AuthorizedProject) -> String {
        let now = SystemTime::now()
            .duration_since(UNIX_EPOCH)
            .map(|d| d.as_millis() as u64)
            .unwrap_or(0);

        let mut store = lock(&self.store);
        store.record(path, &project.display_name, &project.display_path, now);
        // Perder o histórico não é uma falha da operação: só se registra.
        if let Err(error) = store.save(&self.file) {
            self.diagnostics.host(&format!(
                "não foi possível gravar os projetos recentes: {error}"
            ));
        }
        project_id(path)
    }

    pub fn list(&self) -> Vec<RecentProject> {
        self.list_with(|path| Path::new(path).is_dir())
    }

    /// Copia a lista sob o lock e confere as pastas depois dele: um caminho de rede lento não trava quem registra um projeto.
    fn list_with(&self, exists: impl Fn(&str) -> bool) -> Vec<RecentProject> {
        let snapshot = lock(&self.store).clone();
        snapshot.list(exists)
    }

    /// Só volta o que já passou pelo diálogo nativo.
    pub fn path_of(&self, id: &str) -> Option<String> {
        lock(&self.store).find(id).map(|entry| entry.path.clone())
    }

    /// A pasta de um recente que ainda existe. Cada tela explica de um jeito o id fora da lista, por isso a mensagem
    /// vem de quem pede; a pasta que sumiu é dita igual para todas. Confere o disco: pode demorar num caminho de rede.
    pub fn existing_dir(&self, id: &str, not_recent: &str) -> Result<PathBuf, HostError> {
        self.existing_dir_with(id, not_recent, Path::is_dir)
    }

    fn existing_dir_with(
        &self,
        id: &str,
        not_recent: &str,
        exists: impl Fn(&Path) -> bool,
    ) -> Result<PathBuf, HostError> {
        let path = self
            .path_of(id)
            .map(PathBuf::from)
            .ok_or_else(|| HostError::new(error_code::RECENT_NOT_FOUND, not_recent))?;
        if exists(&path) {
            Ok(path)
        } else {
            Err(missing_dir())
        }
    }
}

pub fn missing_dir() -> HostError {
    HostError::new(
        error_code::PATH_NOT_FOUND,
        "A pasta deste projeto não existe mais ou não está acessível.",
    )
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
    fn listar_nao_segura_o_lock_enquanto_confere_as_pastas() {
        // A conferência pode ser lenta (pasta de rede desconectada): quem grava um recente não pode esperar por ela.
        let recents = Recents::load(temp_file("lock-livre"), Diagnostics::default());
        recents.record(
            "C:\\a",
            &AuthorizedProject {
                display_name: "a".into(),
                display_path: "~\\a".into(),
            },
        );
        let livre = std::cell::Cell::new(false);

        let lista = recents.list_with(|_| {
            livre.set(recents.store.try_lock().is_ok());
            true
        });

        assert_eq!(lista.len(), 1);
        assert!(livre.get());
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
    fn a_pasta_do_recente_so_volta_se_estiver_na_lista_e_ainda_existir() {
        let recents = Recents::load(temp_file("pasta"), Diagnostics::default());
        let project = AuthorizedProject {
            display_name: "a".into(),
            display_path: "~".into(),
        };
        let id = recents.record("C:\\a", &project);

        let found = recents.existing_dir_with(&id, "fora", |_| true);
        let gone = recents
            .existing_dir_with(&id, "fora", |_| false)
            .unwrap_err();
        let unknown = recents
            .existing_dir_with("r-0000000000000000", "fora da lista", |_| true)
            .unwrap_err();

        assert_eq!(found.unwrap(), PathBuf::from("C:\\a"));
        assert_eq!(gone.code, error_code::PATH_NOT_FOUND);
        assert_eq!(
            (unknown.code.as_str(), unknown.message.as_str()),
            (error_code::RECENT_NOT_FOUND, "fora da lista")
        );
        let _ = fs::remove_dir_all(temp_file("pasta").parent().unwrap());
    }

    #[test]
    fn falha_ao_gravar_os_recentes_vai_para_o_diagnostico_redigida() {
        // A pasta de dados é um arquivo: criar a lista dentro dela falha.
        let blocker = temp_file("bloqueado");
        fs::create_dir_all(blocker.parent().unwrap()).unwrap();
        fs::write(&blocker, "").unwrap();
        let diagnostics = Diagnostics::default();
        let recents = Recents::load(blocker.join(FILE_NAME), diagnostics.clone());

        recents.record(
            "C:\\a",
            &AuthorizedProject {
                display_name: "a".into(),
                display_path: "~".into(),
            },
        );

        let entries = diagnostics.snapshot();
        assert_eq!(entries.len(), 1, "{entries:?}");
        assert!(entries[0]
            .message
            .starts_with("não foi possível gravar os projetos recentes"));
        let folder = blocker.parent().unwrap().to_string_lossy().into_owned();
        assert!(!entries[0].message.contains(&folder));
        let _ = fs::remove_dir_all(blocker.parent().unwrap());
    }

    #[test]
    fn so_se_reabre_o_que_esta_na_lista() {
        let mut store = RecentStore::default();
        store.record("C:\\a", "a", "~", 1);

        assert!(store.find(&project_id("C:\\a")).is_some());
        assert!(store.find(&project_id("C:\\Windows")).is_none());
        assert!(store.find("r-0000000000000000").is_none());
    }

    #[test]
    fn a_selecao_registrada_leva_o_id_do_recente_correspondente() {
        let file = temp_file("selecao");
        let recents = Recents::load(file.clone(), Diagnostics::default());
        let authorized = serde_json::json!({
            "projectId": "p-000000000000",
            "displayName": "app",
            "displayPath": "~/app"
        });

        let recent_id = recents.record(
            "/projetos/app",
            &AuthorizedProject::from_engine(&authorized),
        );

        let listed = recents.list();
        assert_eq!(recent_id, listed[0].id);
        assert_eq!(
            recents.path_of(&listed[0].id).as_deref(),
            Some("/projetos/app")
        );
        let _ = fs::remove_dir_all(file.parent().unwrap());
    }
}
