"""Os nós do grafo management.investigation. Cada um recebe o estado, devolve o estado e não conhece a ordem dos outros.

Os fatos e o modelo vêm de ``Run``, não do estado: o estado guarda só ids, nomes de grupo e contagens, e é o que o motor
registra como caminho da investigação. Texto de fato e texto de hipótese não entram no caminho.
"""

from __future__ import annotations

import unicodedata
from dataclasses import dataclass, field
from typing import Callable

from agents import investigation as agent
from agents import prompts
from contracts.investigation import MAX_SEARCHES, Fact, InvestigationRequest
from knowledge import retrieval
from knowledge.index import KnowledgeError, sync
from workflows.engineering.budget import model_call
from workflows.engineering.state import Segment, limit

# Uma rodada é um olhar: reunir fatos do motor ou buscar na memória do projeto.
MAX_ROUNDS = 4
MAX_HITS = 3
MEMORY_GROUP = "memória"
MAX_GATHERED = 120
MAX_DETERMINISTIC_SIGNS = 6
# Os grupos que dão o chão da resposta (a data de hoje, o período, a frescura dos dados) entram sempre.
BASE_GROUPS = ("contexto", "fontes", "nao calculado")
FALLBACK_GROUPS = ("indicadores", "riscos")
# Grupos cujos fatos são sinais de atenção: sem modelo, a resposta os lista como foram calculados.
SIGN_GROUPS = ("riscos", "execucoes")
NO_MODEL = "INVESTIGATION_NO_MODEL"

# Para onde olhar primeiro, pelas palavras da pergunta (sem acento, em minúsculas). Só escolhe por onde começar:
# o que mais for preciso o modelo pede, e o laço tem teto.
KEYWORDS: dict[str, tuple[str, ...]] = {
    "indicadores": ("vazao", "throughput", "entreg", "lead time", "cycle", "fluxo", "andamento", "wip", "caiu", "queda", "ritmo", "velocidade", "lento", "devagar"),
    "previsao": ("previsao", "prazo", "quando", "terminar", "estimativa", "atras"),
    "riscos": ("risco", "bloque", "parad", "atras", "vencid", "problema", "gargalo"),
    "marcos": ("marco", "release", "versao", "milestone"),
    "execucoes": ("execucao", "execucoes", "falha", "falhou", "erro", "agente", "agentes", "pipeline"),
    "mudancas": ("mudou", "mudanca", "alteracao", "desde", "passou a", "depois de"),
    "fontes": ("sincroniz", "fonte", "dados", "atualizad"),
}


@dataclass
class Run:
    """O que vale só para esta investigação: os fatos do motor, o trecho de execução e o modelo."""

    request: InvestigationRequest
    segment: Segment
    # Os trechos que as buscas trouxeram viram fatos citáveis (``memory.N``); o motor confere cada um contra o documento dele.
    found: dict[str, Fact] = field(default_factory=dict)
    synced: bool = False

    @property
    def facts(self) -> dict[str, Fact]:
        return {**{fact.id: fact for fact in self.request.facts}, **self.found}

    @property
    def can_search(self) -> bool:
        return self.request.memory is not None and bool(self.request.memory.documents)


def plain(text: str) -> str:
    """Minúsculas e sem acento, para comparar nomes de grupo e palavras da pergunta."""
    return "".join(ch for ch in unicodedata.normalize("NFKD", text.lower()) if not unicodedata.combining(ch))


def focus(question: str, groups: list[str]) -> list[str]:
    """Por onde começar: os grupos de base mais os que as palavras da pergunta indicam; sem palavra reconhecida, fluxo e riscos."""
    asked = plain(question)
    by_plain = {plain(group): group for group in groups}
    base = [by_plain[name] for name in BASE_GROUPS if name in by_plain]
    matched = [by_plain[name] for name, stems in KEYWORDS.items() if name in by_plain and name not in BASE_GROUPS and any(stem in asked for stem in stems)]
    if not matched:
        matched = [by_plain[name] for name in FALLBACK_GROUPS if name in by_plain] or [group for group in groups if group not in base][:2]
    return list(dict.fromkeys([*base, *matched]))


def frame(state: dict, run: Run) -> dict:
    groups = focus(run.request.question, run.request.groups)
    state["next_groups"] = groups
    _step(state, "frame", "Pergunta enquadrada", f"Começa por: {', '.join(groups)}.", groups=groups)
    return state


def gather(state: dict, run: Run) -> dict:
    consulted = list(state.get("consulted") or [])
    fresh = [group for group in state.get("next_groups") or [] if group not in consulted]
    gathered = list(state.get("gathered") or [])
    added = [fact.id for fact in run.request.facts if fact.group in fresh and fact.id not in gathered][:MAX_GATHERED]
    state["consulted"] = [*consulted, *fresh]
    state["gathered"] = [*gathered, *added]
    state["round"] = int(state.get("round") or 0) + 1
    state["next_groups"] = []
    _step(
        state, "gather", f"Fatos reunidos (rodada {state['round']})",
        f"Grupos: {', '.join(fresh) or 'nenhum'}; {_count(len(added), 'fato', 'fatos')}.", round=state["round"], groups=fresh, facts=added,
    )
    return state


def hypothesize(state: dict, run: Run) -> dict:
    state["needs"] = []
    if run.segment.model is None:
        limit(state, NO_MODEL, "Sem modelo autorizado, a investigação só reuniu os fatos e listou os sinais.")
        _step(state, "hypothesize", f"Hipóteses (rodada {state['round']})", "Sem modelo: etapa não executada.", round=state["round"])
        return state
    facts = run.facts
    gathered = [facts[fact_id] for fact_id in state["gathered"]]

    state["searches"] = []
    searched = tuple(state.get("searched") or [])
    can_search = run.can_search and len(searched) < MAX_SEARCHES

    def adopt(outcome) -> None:
        state["hypotheses"] = outcome.value["hypotheses"]
        state["needs"] = outcome.value["needs"]
        state["searches"] = outcome.value["searches"][: MAX_SEARCHES - len(searched)]

    def work() -> int:
        return model_call(
            state, run.segment,
            lambda can_spend: agent.hypothesize(
                run.segment.model, run.request.question, gathered, run.request.groups, state["consulted"], can_spend, can_search, searched),
            adopt, "INVESTIGATION_HYPOTHESES_FALLBACK", "As hipóteses não vieram do modelo.",
        )

    _agent_call(state, run.segment, work)
    count = len(state.get("hypotheses") or [])
    asks = state["needs"]
    asked = _asked(asks, len(state["searches"]))
    _step(
        state, "hypothesize", f"Hipóteses (rodada {state['round']})",
        _count(count, "hipótese", "hipóteses") + (f"; pede: {asked}." if asked else "; não pede mais fatos."), round=state["round"],
    )
    return state


def _rounds_needed(wanted: list[str], searches: list[str]) -> int:
    """Buscar na memória e reunir grupos são duas rodadas (o grafo faz `retrieve` e depois `gather`), uma só se pedir apenas um deles."""
    return bool(searches) + bool(wanted)


def decide(state: dict, run: Run) -> dict:
    """Reunir mais ou concluir. Concluir quando nada mais foi pedido, no teto de rodadas ou quando o orçamento não cobre outra volta."""
    segment = run.segment
    present = {fact.group for fact in run.request.facts}
    wanted = [group for group in state.get("needs") or [] if group not in (state.get("consulted") or []) and group in present]
    searches = list(state.get("searches") or [])
    if segment.model is None:
        reason = "Sem modelo, não há o que cruzar a mais."
    elif state.get("failure"):
        reason = "A investigação falhou."
    elif not wanted and not searches:
        reason = "Os fatos reunidos bastam: nada mais foi pedido."
    elif state["round"] + _rounds_needed(wanted, searches) > MAX_ROUNDS:
        reason = f"Teto de {MAX_ROUNDS} rodadas."
    elif int(state.get("iterations") or 0) + 3 > segment.max_iterations:
        reason = "Não restam etapas para outra rodada."
    elif int(state.get("tokens") or 0) + 2 * agent.MAX_OUTPUT_TOKENS > segment.max_tokens:
        reason = "O orçamento de tokens não cobre outra rodada."
    else:
        state["next_groups"] = wanted
        _step(state, "decide", "Decisão: olhar mais", f"Pede: {_asked(wanted, len(searches))}.", round=state["round"], groups=wanted)
        return state
    state["next_groups"] = []
    state["searches"] = []
    if (wanted or searches) and segment.model is not None:
        limit(state, "INVESTIGATION_STOPPED", f"A investigação parou antes de olhar: {_asked(wanted, len(searches))}. {reason}")
    _step(state, "decide", "Decisão: concluir", reason, round=state["round"])
    return state


def retrieve(state: dict, run: Run) -> dict:
    """
    Busca na memória do projeto (o laço do RAG): cada consulta que o modelo propôs vira trechos, e cada trecho vira um fato
    citável. A consulta em si não vai para o caminho (é texto do modelo, derivado da pergunta): só quantas foram.
    """
    done = list(state.get("searched") or [])
    queries = [query for query in state.get("searches") or [] if query][: MAX_SEARCHES - len(done)]
    state["searches"] = []
    memory = run.request.memory
    added: list[str] = []
    retrieved = list(state.get("retrieved") or [])
    if memory is not None and queries:
        try:
            if not run.synced:
                sync(memory.root, run.request.project_id, memory.documents)
                run.synced = True
            for query in queries:
                for hit in retrieval.search(memory.root, run.request.project_id, query, MAX_HITS):
                    if any(item["chunkId"] == hit["chunkId"] for item in retrieved):
                        continue
                    fact_id = f"memory.{len(retrieved) + 1}"
                    retrieved.append({"id": fact_id, "path": hit["filePath"], "chunkId": hit["chunkId"]})
                    run.found[fact_id] = Fact(fact_id, MEMORY_GROUP, _memory_text(hit))
                    added.append(fact_id)
        except KnowledgeError:
            limit(state, "INVESTIGATION_MEMORY_UNAVAILABLE", "A memória do projeto não pôde ser consultada.")
    state["retrieved"] = retrieved
    state["searched"] = [*done, *queries]
    state["gathered"] = [*(state.get("gathered") or []), *added]
    if MEMORY_GROUP not in (state.get("consulted") or []):
        state["consulted"] = [*(state.get("consulted") or []), MEMORY_GROUP]
    state["round"] = int(state.get("round") or 0) + 1
    _step(
        state, "retrieve", f"Memória do projeto consultada (rodada {state['round']})",
        f"{_count(len(queries), 'busca', 'buscas')}; {_count(len(added), 'trecho', 'trechos')}.", round=state["round"], groups=[MEMORY_GROUP], facts=added,
    )
    return state


def _memory_text(hit: dict) -> str:
    """O trecho como fato, com a natureza do documento dita: um item sincronizado é escrito por terceiros."""
    external = str(hit.get("filePath", "")).startswith(prompts.EXTERNAL_PREFIX)
    label = "item sincronizado, escrito por terceiros" if external else "decisão registrada pela equipe"
    return f"Memória do projeto ({label}): {' '.join(str(hit.get('excerpt', '')).split())}"[:400]


def _asked(groups: list[str], searches: int) -> str:
    parts = ([f"fatos de {', '.join(groups)}"] if groups else []) + ([_count(searches, "busca na memória", "buscas na memória")] if searches else [])
    return " e ".join(parts)


def conclude(state: dict, run: Run) -> dict:
    facts = run.facts
    gathered = [facts[fact_id] for fact_id in state["gathered"]]
    state["rejected"] = []
    state["answer"] = None
    written = False
    if run.segment.model is not None:
        def adopt(outcome) -> None:
            nonlocal written
            written = True
            state["answer"] = {key: value for key, value in outcome.value.items() if key not in ("schema", "rejected")}
            state["rejected"] = outcome.value["rejected"]

        def work() -> int:
            return model_call(
                state, run.segment,
                lambda can_spend: agent.conclude(run.segment.model, run.request.question, gathered, state.get("hypotheses") or [], can_spend),
                adopt, "INVESTIGATION_CONCLUSION_FALLBACK", "A resposta veio das regras, não do modelo.",
            )

        _agent_call(state, run.segment, work)
    if not written:
        state["answer"] = _signs_only(gathered)
    _step(
        state, "conclude", "Conclusão",
        "Escrita pelo modelo, a conferir pelo motor." if written else "Montada pelas regras: só os sinais reunidos, sem texto do modelo.",
    )
    state["valid"] = True
    return state


def _signs_only(gathered: list[Fact]) -> dict:
    """Sem texto do modelo: os sinais de risco e de execução como o motor os calculou, cada um citando o próprio fato."""
    signs = [fact for fact in gathered if plain(fact.group) in SIGN_GROUPS][:MAX_DETERMINISTIC_SIGNS]
    cite = [signs[0].id] if signs else [gathered[0].id] if gathered else []
    return {
        "summary": {"text": "Sem o modelo, esta investigação só reuniu os fatos e listou os sinais de atenção.", "evidence": cite},
        "observed": [{"text": fact.text[:400], "evidence": [fact.id]} for fact in signs],
        "calculations": [],
        "inferences": [],
        "recommendations": [],
        "unavailable": [{"text": "A análise que cruza os fatos depende do modelo e não foi escrita."}],
    }


def _count(value: int, one: str, many: str) -> str:
    return f"{value} {one if value == 1 else many}"


def _step(state: dict, step_id: str, title: str, detail: str, *, round: int = 0, groups: list[str] | None = None, facts: list[str] | None = None) -> None:
    state["path"] = [
        *(state.get("path") or []),
        {"id": step_id, "title": title, "detail": detail, "round": round, "groups": groups or [], "facts": facts or []},
    ]


def _agent_call(state: dict, segment: Segment, work: Callable[[], int]) -> None:
    """Anuncia o começo e o fim de uma chamada do agente; cada rodada é um par de eventos."""
    started = segment.clock()
    segment.emitter.emit("agent.started", {"agentVersion": agent.AGENT_VERSION}, agent.AGENT_ID)
    tokens = work()
    segment.emitter.emit(
        "agent.completed", {"status": "completed", "durationMs": int((segment.clock() - started) * 1000), "tokens": tokens}, agent.AGENT_ID,
    )


__all__ = ["BASE_GROUPS", "KEYWORDS", "MAX_ROUNDS", "NO_MODEL", "Run", "conclude", "decide", "focus", "frame", "gather", "hypothesize", "plain", "retrieve"]
