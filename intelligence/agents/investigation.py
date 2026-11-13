"""O agente de investigação de entrega: duas chamadas com modelo, a das hipóteses e a da conclusão.

Os fatos são do motor (calculados por ele, cada um com um id citável). O modelo só cruza esses fatos: não faz conta nova,
não usa conhecimento de fora e cita os ids que sustentam cada afirmação. Os dois pedidos têm validação própria; a
conferência dos números da resposta final continua no motor.
"""

from __future__ import annotations

from agents import prompts
from agents.runner import Model, Outcome, ask
from contracts.investigation import (
    CONCLUSION_SCHEMA,
    HYPOTHESES_SCHEMA,
    MAX_HYPOTHESES,
    MAX_NEEDS,
    MAX_SEARCHES_PER_ROUND,
    Fact,
    conclusion_from_model,
    hypotheses_from_model,
)

AGENT_ID = "investigator"
AGENT_VERSION = "1"
MAX_OUTPUT_TOKENS = 1200
PURPOSE = "investigate"

_HYPOTHESES_TASK = (
    "Você investiga uma pergunta de gestão de entrega de software a partir de fatos que o sistema já calculou, cada um com um "
    "id entre colchetes. Use SOMENTE esses fatos: nenhum conhecimento externo e nenhuma conta nova. "
    f"Proponha de 0 a {MAX_HYPOTHESES} hipóteses que expliquem o que a pergunta quer saber, cada uma sustentada por fatos "
    "reunidos. Se faltarem fatos de outro grupo para confirmar ou descartar uma hipótese, peça esse grupo em 'needs'. Peça só "
    f"grupos da lista de grupos disponíveis que ainda não foram consultados, no máximo {MAX_NEEDS}. Formato exato, em JSON: "
    f'{{"schema":"{HYPOTHESES_SCHEMA}","hypotheses":[{{"text":"hipótese em uma frase, em linguagem de hipótese","evidence":["id"]}}],'
    '"needs":["grupo"]}. '
    "Em 'evidence' cite só ids que aparecem nos fatos. Uma hipótese sem fato que a sustente não entra."
)

# Só entra no pedido quando o motor mandou memória do projeto (decisões registradas e itens autorizados).
_SEARCH_TASK = (
    " Há também a memória do projeto: decisões registradas pela equipe e itens sincronizados. Se saber o que a equipe já decidiu "
    f"ou o que dizem os itens ajudaria, proponha em 'searches' até {MAX_SEARCHES_PER_ROUND} buscas curtas, de palavras-chave "
    "(sem frases inteiras e sem repetir uma busca já feita). O resultado volta como fatos com id 'memory.N', que você pode citar. "
    'Acrescente ao JSON: "searches":["palavras-chave"]. Sem necessidade, deixe a lista vazia.'
)

_CONCLUSION_TASK = (
    "Você escreve a resposta final de uma investigação de gestão de entrega de software, em português do Brasil, com frases "
    "curtas e diretas. Use SOMENTE os fatos fornecidos, cada um com um id entre colchetes. Todo número que você escrever "
    "precisa aparecer nos fatos que você citar. Separe a resposta assim: observed (o que os dados mostram), calculations (o que "
    "o sistema calculou), inferences (interpretações suas, em linguagem de hipótese: 'pode indicar', 'sugere'), "
    "recommendations (ações possíveis, ligadas aos fatos) e unavailable (o que a pergunta pede e os fatos não têm: diga isso em "
    "vez de supor). Cada item, e o resumo, cita em 'evidence' os ids dos fatos que o sustentam. Use as hipóteses consideradas "
    "só como pista do que cruzar: uma hipótese sem fato que a confirme vira 'unavailable' ou fica de fora. Formato exato, em "
    f'JSON: {{"schema":"{CONCLUSION_SCHEMA}","summary":{{"text":"uma ou duas frases","evidence":["id"]}},'
    '"observed":[{"text":"...","evidence":["id"]}],"calculations":[],"inferences":[],"recommendations":[],'
    '"unavailable":[{"text":"..."}]}.'
)


def _entry(fact: Fact) -> str:
    return f"[{fact.id}] ({fact.group}) {prompts.neutralize(fact.text)}"


def fit(facts: list[Fact]) -> tuple[list[Fact], int]:
    """Os fatos que cabem no bloco do pedido, na ordem em que chegaram, e quantos ficaram de fora (os últimos)."""
    kept: list[Fact] = []
    size = 0
    for fact in facts:
        size += len(_entry(fact))
        if size > prompts.MAX_BLOCK:
            break
        kept.append(fact)
    return kept, len(facts) - len(kept)


def facts_block(facts: list[Fact]) -> str:
    """Os fatos que cabem, cada um com o seu id citável, dentro do bloco delimitado. O texto pode trazer título de item escrito por terceiros."""
    parts = [_entry(fact) for fact in fit(facts)[0]]
    body = "\n".join(parts) if parts else "(nenhum fato reunido)"
    return f"<{prompts.TAG}>\n{body}\n</{prompts.TAG}>"


def hypothesize(model: Model, question: str, gathered: list[Fact], groups: list[str], consulted: list[str], can_spend,
                can_search: bool = False, searched: tuple[str, ...] = ()) -> Outcome:
    known = {fact.id for fact in fit(gathered)[0]}
    pending = [group for group in groups if group not in consulted]
    user = (
        f"{prompts.request_block(question)}\n\nGrupos já consultados: {', '.join(consulted) or 'nenhum'}.\n"
        f"Grupos disponíveis que ainda não foram consultados: {', '.join(pending) or 'nenhum'}.\n\n{facts_block(gathered)}"
    )
    return ask(
        model,
        agent_id=AGENT_ID,
        purpose=PURPOSE,
        messages=[prompts.system(_HYPOTHESES_TASK + (_SEARCH_TASK if can_search else "")), {"role": "user", "content": user}],
        max_tokens=MAX_OUTPUT_TOKENS,
        response_schema=HYPOTHESES_SCHEMA,
        parse=lambda document: hypotheses_from_model(document, known, set(groups), can_search, searched),
        can_spend=can_spend,
    )


def conclude(model: Model, question: str, gathered: list[Fact], hypotheses: list[dict], can_spend) -> Outcome:
    known = {fact.id for fact in fit(gathered)[0]}
    considered = "\n".join(f"- {item['text']}" for item in hypotheses) or "(nenhuma)"
    user = f"{prompts.request_block(question)}\n\nHipóteses consideradas:\n{prompts.neutralize(considered)}\n\n{facts_block(gathered)}"
    return ask(
        model,
        agent_id=AGENT_ID,
        purpose=PURPOSE,
        messages=[prompts.system(_CONCLUSION_TASK), {"role": "user", "content": user}],
        max_tokens=MAX_OUTPUT_TOKENS,
        response_schema=CONCLUSION_SCHEMA,
        parse=lambda document: conclusion_from_model(document, known),
        can_spend=can_spend,
    )
