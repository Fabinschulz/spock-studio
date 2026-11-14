# Validação real

Quase tudo o que o Spock faz com IA e com integrações foi provado só com dublês: um provedor de modelo roteirizado, um
GitHub e um Jira falsos. Este documento diz o que falta provar com dados reais, como provar e onde registrar o resultado.

## O que ainda não foi provado

| Área | O que os testes de hoje provam | O que só um dado real prova | Teste ao vivo |
| --- | --- | --- | --- |
| Sincronização do GitHub | Paginação, retomada e idempotência com respostas escritas no teste | Limite de chamadas, campos reais, repositório grande | `LiveValidationTests` |
| Sincronização do Jira | O conector inteiro, só com dublês | Campos que variam por site, JQL real, paginação real | `LiveValidationTests` |
| Chave do modelo no cofre e assistente gerencial | O verificador aceita e descarta afirmações roteirizadas | Quanto o verificador descarta de uma resposta real | `LiveValidationTests` |
| Investigação de gestão (LangGraph) | O grafo e o pontuador com respostas de referência | Nota das respostas de um modelo real, por provedor | `ManagementEvalLiveTests` |
| Da ideia ao projeto (fase A) | Checagens e segunda tentativa com respostas roteirizadas | Quantas etapas um modelo real escreve de um jeito que as checagens aceitam, e com quantas tentativas | `IdeaLiveTests` |
| Cofre no Linux e no macOS | A lógica, com um cofre falso | O `secret-tool` e o Keychain de verdade | `UnixVaultRealityTests` (rodar na máquina) |

Ficam fora daqui, porque pedem uma pessoa na frente da tela: a conferência das telas no Tauri com leitor de tela, zoom e
alto contraste do Windows.

## Como rodar

Defina as variáveis no seu terminal, nunca no chat nem em arquivo. Basta definir os grupos que quiser validar; o que
ficar sem variável aparece como ignorado, nunca como aprovado.

```powershell
$env:MARILIZ_LIVE_GITHUB_TOKEN = "..."; $env:MARILIZ_LIVE_GITHUB_REPO = "dono/repositorio"
$env:MARILIZ_LIVE_JIRA_SITE = "https://equipe.atlassian.net"; $env:MARILIZ_LIVE_JIRA_EMAIL = "..."
$env:MARILIZ_LIVE_JIRA_TOKEN = "..."; $env:MARILIZ_LIVE_JIRA_PROJECT = "CHAVE"
$env:MARILIZ_LIVE_OPENAI_KEY = "..."
$env:MARILIZ_LIVE_ANTHROPIC_KEY = "..."
$env:MARILIZ_LIVE_GOOGLE_KEY = "..."
python scripts/live-validation.py
```

O script mostra o que vai rodar e o que fica ignorado, roda os três conjuntos de testes ao vivo, pontua as respostas das
evals de gestão com `intelligence/evaluations/management.py` e grava o relatório numa pasta temporária (ou em `--out`).
Antes de terminar ele confere que nenhum token ou chave foi parar nos arquivos da pasta. O relatório traz só contagens,
estados, tentativas e tokens.

Use credenciais de teste: um repositório e um projeto do Jira sem dado sensível, e chaves com limite de gasto.

## Resultado

Preencha depois de cada rodada, inclusive o que falhou. Uma falha aqui é o objetivo da validação, não um problema dela.

| Data | Grupo | Resultado | Observação |
| --- | --- | --- | --- |
| | GitHub | não rodado | |
| | Jira | não rodado | |
| | Modelo e assistente (OpenAI) | não rodado | |
| | Evals de gestão (OpenAI, Anthropic) | não rodado | |
| | Fase A (OpenAI, Anthropic, Gemini) | não rodado | |
| | Cofre no Linux e no macOS | não rodado | |
