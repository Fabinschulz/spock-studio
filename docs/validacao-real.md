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
| Reconhecimento de intenção do Boot por modelo (BD-1) | O encanamento e as garantias, com um modelo dublê que responde por regra: só onde o léxico não resolve, ids conferidos no catálogo, falha igual ao léxico, sem segredo nem texto no registro | Quanto o modelo acerta nas frases que o léxico erra (holdout), quanto ele estraga, quantos tokens gasta por pedido e quanto demora | `BootIntentModelEvalTests` (`Holdout_ao_vivo_com_a_OpenAI`, `Holdout_ao_vivo_com_a_Anthropic`) |

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

O reconhecimento de intenção do Boot por modelo tem um teste ao vivo próprio, que não passa pelo script acima. Com a chave
definida no terminal (`MARILIZ_LIVE_OPENAI_KEY` ou `MARILIZ_LIVE_ANTHROPIC_KEY`):

```powershell
dotnet test runtime/tests/Mariliz.Application.Tests --filter "FullyQualifiedName~BootIntentModelEvalTests" --logger "console;verbosity=detailed"
```

Ele roda as 14 frases do holdout da eval de intenção (nunca usadas para ajustar os termos) uma vez só pelo léxico e outra com
o modelo nos casos em que o léxico não resolve, e escreve no resultado do teste o acerto das duas medidas, o que o modelo
acertou e o que estragou, o número de chamadas, os tokens de entrada e de saída e o tempo. São no máximo 14 chamadas curtas
(até 120 tokens de saída). A pergunta que ele responde: vale manter o segundo estágio? O número do dublê que roda sem chave
no CI não responde isso.

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
| | Reconhecimento de intenção por modelo (OpenAI, Anthropic) | não rodado | |
