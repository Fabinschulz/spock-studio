//! A leitura do terminal chega em pedaços de tamanho qualquer: um acento pode ficar com metade em cada pedaço. O que
//! está incompleto espera o próximo pedaço em vez de virar um caractere inválido na tela.

#[derive(Default)]
pub struct Utf8Stream {
    pending: Vec<u8>,
}

impl Utf8Stream {
    pub fn push(&mut self, bytes: &[u8]) -> String {
        self.pending.extend_from_slice(bytes);
        match std::str::from_utf8(&self.pending) {
            Ok(text) => {
                let text = text.to_string();
                self.pending.clear();
                text
            }
            Err(error) if error.error_len().is_none() => {
                let complete = error.valid_up_to();
                let text = String::from_utf8_lossy(&self.pending[..complete]).into_owned();
                self.pending.drain(..complete);
                text
            }
            Err(_) => {
                let text = String::from_utf8_lossy(&self.pending).into_owned();
                self.pending.clear();
                text
            }
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn um_acento_partido_ao_meio_sai_inteiro_no_pedaco_seguinte() {
        let bytes = "ação".as_bytes();
        let mut stream = Utf8Stream::default();

        let first = stream.push(&bytes[..2]);
        let second = stream.push(&bytes[2..]);

        assert_eq!(first, "a");
        assert_eq!(second, "ção");
    }

    #[test]
    fn byte_invalido_vira_o_caractere_de_substituicao_sem_travar_a_saida() {
        let mut stream = Utf8Stream::default();

        assert_eq!(stream.push(&[b'o', 0xff, b'k']), "o\u{fffd}k");
        assert_eq!(stream.push(b"!"), "!");
    }
}
