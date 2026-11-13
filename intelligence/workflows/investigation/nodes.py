"""Os nós do grafo management.investigation. Cada um recebe o estado, devolve o estado e não conhece a ordem dos outros.

Os fatos e o modelo vêm de ``Run``, não do estado: o estado guarda só ids, nomes de grupo e contagens, e é o que o motor
registra como caminho da investigação. Texto de fato e texto de hipótese não entram no caminho.
"""

from __future__ import annotations

import unicodedata
from dataclasses import dataclass
from typing import Callable

from agents import investigation as agent
from contracts.investigation import Fact, InvestigationRequest
from workflows.engineering.budget import model_call
from workflows.engineering.state import Segment, limit

MAX_ROUNDS = 3
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

    @property
    def facts(self) -> dict[str, Fact]:
        return {fact.id: fact for fact in self.request.facts}


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

    def adopt(outcome) -> None:
        state["hypotheses"] = outcome.value["hypotheses"]
        state["needs"] = outcome.value["needs"]

    def work() -> int:
        return model_call(
            state, run.segment,
            lambda can_spend: agent.hypothesize(run.segment.model, run.request.question, gathered, run.request.groups, state["consulted"], can_spend),
            adopt, "INVESTIGATION_HYPOTHESES_FALLBACK", "As hipóteses não vieram do modelo.",
        )

    _agent_call(state, run.segment, work)
    count = len(state.get("hypotheses") or [])
    asks = state["needs"]
    _step(
        state, "hypothesize", f"Hipóteses (rodada {state['round']})",
        _count(count, "hipótese", "hipóteses") + (f"; pede: {', '.join(asks)}." if asks else "; não pede mais fatos."), round=state["round"],
    )
    return state


def decide(state: dict, run: Run) -> dict:
    """Reunir mais ou concluir. Concluir quando nada mais foi pedido, no teto de rodadas ou quando o orçamento não cobre outra volta."""
    segment = run.segment
    present = {fact.group for fact in run.request.facts}
    wanted = [group for group in state.get("needs") or [] if group not in (state.get("consulted") or []) and group in present]
    if segment.model is None:
        reason = "Sem modelo, não há o que cruzar a mais."
    elif state.get("failure"):
        reason = "A investigação falhou."
    elif not wanted:
        reason = "Os fatos reunidos bastam: nenhum outro grupo foi pedido."
    elif state["round"] >= MAX_ROUNDS:
        reason = f"Teto de {MAX_ROUNDS} rodadas."
    elif int(state.get("iterations") or 0) + 3 > segment.max_iterations:
        reason = "Não restam etapas para outra rodada."
    elif int(state.get("tokens") or 0) + 2 * agent.MAX_OUTPUT_TOKENS > segment.max_tokens:
        reason = "O orçamento de tokens não cobre outra rodada."
    else:
        state["next_groups"] = wanted
        _step(state, "decide", "Decisão: reunir mais fatos", f"Pede: {', '.join(wanted)}.", round=state["round"], groups=wanted)
        return state
    state["next_groups"] = []
    if wanted and segment.model is not None:
        limit(state, "INVESTIGATION_STOPPED", f"A investigação parou antes de olhar: {', '.join(wanted)}. {reason}")
    _step(state, "decide", "Decisão: concluir", reason, round=state["round"])
    return state


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


__all__ = ["BASE_GROUPS", "KEYWORDS", "MAX_ROUNDS", "NO_MODEL", "Run", "conclude", "decide", "focus", "frame", "gather", "hypothesize", "plain"]
