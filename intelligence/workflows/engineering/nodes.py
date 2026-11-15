from __future__ import annotations

import hashlib
import sys

from langgraph.types import interrupt

from agents import architecture as architecture_model
from agents import judge as judge_model
from agents import planning as planning_model
from agents.runner import Outcome
from contracts.engineering import (
    ACTION_FILE_WRITE,
    CRITERIA_VERSION,
    JUDGE_EVAL_ID,
    decision_intent,
    forbidden,
    impact_ok,
    judgment_outcome,
)
from knowledge import retrieval
from knowledge.index import KnowledgeError, sync
from workflows.engineering.budget import CanSpend, model_call, run_agent
from workflows.engineering.state import (
    AGENT_VERSION,
    ARCHITECTURE,
    AREA_BY_STACK,
    EVALUATION,
    PLANNING,
    Segment,
    fail,
    limit,
    text,
)

_RAG_NOT_READY = "A recuperação de contexto ainda não indexa o projeto."


def request_analyzer(state: dict) -> dict:
    if not state["goal"]:
        limit(state, "REQUEST_EMPTY", "O pedido veio sem objetivo.")
    return state


def context_builder(state: dict) -> dict:
    # Só aceita identificadores curtos vindos do motor. Texto livre do pedido não entra aqui.
    return state


def rag_research(state: dict, segment: Segment) -> dict:
    state["evidence"] = []
    state["retrieval_attempted"] = False
    root = state["knowledge_root"]
    if not root or segment.sources is None:
        limit(state, "RAG_NOT_READY", _RAG_NOT_READY)
        return state
    segment.emitter.emit("context.retrieval.started", {"strategy": ["lexical", "symbol"]})
    try:
        stats = sync(root, state["project_id"], segment.sources)
        hits = retrieval.search(root, state["project_id"], state["goal"])
    except KnowledgeError as ex:
        # A causa vai para o diagnóstico do host pelo stderr; a mensagem do SQLite fica de fora porque pode trazer caminho.
        cause = type(ex.__cause__).__name__ if ex.__cause__ else type(ex).__name__
        print(f"[ai] índice indisponível na recuperação: {cause}", file=sys.stderr, flush=True)
        limit(state, "RAG_NOT_READY", _RAG_NOT_READY)
        return state
    state["retrieval_attempted"] = True
    state["evidence"] = hits
    for hit in hits:
        segment.emitter.emit("context.retrieved", {
            "documentId": hit["documentId"],
            "chunkId": hit["chunkId"],
            "filePath": hit["filePath"],
            "hash": hit["hash"],
            "score": hit["score"],
            "retriever": hit.get("retriever", "lexical"),
        })
    _limit_retrieval(state, stats, hits)
    return state


def _limit_retrieval(state: dict, stats: dict, hits: list[dict]) -> None:
    limit(state, "EMBEDDINGS_NOT_READY", "A busca é híbrida (texto e símbolos). Embeddings ficam para quando a avaliação mostrar ganho.")
    if not stats["valid"]:
        limit(state, "INDEX_INVALID", "Parte do índice não passou na validação e será refeita na próxima execução.")
    if stats["pending"]:
        limit(state, "INDEX_PENDING", "Parte do projeto ainda não foi indexada; entra nas próximas execuções.")
    if hits:
        return
    if stats["documents"] == 0:
        limit(state, "RAG_EMPTY", "Não há fontes indexáveis neste projeto.")
    else:
        limit(state, "RAG_NO_HITS", "Nenhum trecho do projeto corresponde ao pedido.")


def planning_agent(state: dict, segment: Segment | None = None) -> dict:
    steps = [{"id": "describe", "title": "Descrever o pedido"}]
    if state["stacks"]:
        steps.append({"id": "context", "title": "Considerar a stack detectada"})
    if state["evidence"]:
        steps.append({"id": "evidence", "title": "Usar trechos recuperados"})
    if state["asks_impact"]:
        steps.append({"id": "impact", "title": "Avaliar o impacto técnico"})
    state["plan"] = {"schema": "engineering.plan.v1", "steps": steps}

    if segment is not None and segment.model is not None:
        def work() -> int:
            return model_call(
                state, segment, lambda can_spend: planning_model.plan(segment.model, state, can_spend),
                lambda outcome: state.__setitem__("plan", outcome.value),
                "PLAN_MODEL_FALLBACK", "O plano veio das regras determinísticas, não do modelo.")

        run_agent(state, segment, PLANNING, planning_model.AGENT_VERSION, work)
    return state


def plan_validator(state: dict) -> dict:
    plan = state.get("plan")
    steps = plan.get("steps") if isinstance(plan, dict) else None
    valid = (
        isinstance(plan, dict)
        and plan.get("schema") == "engineering.plan.v1"
        and isinstance(steps, list)
        and bool(steps)
        and not forbidden(plan)
        and all(isinstance(step, dict) and text(step.get("id")) and text(step.get("title")) for step in steps)
    )
    state["valid"] = valid
    if not valid:
        fail(state, "PLAN_INVALID", "O plano não respeita o contrato.")
    return state


def architecture_agent(state: dict, segment: Segment | None = None) -> dict:
    def work() -> int:
        state["impact"] = _deterministic_impact(state)
        if segment is None or segment.model is None:
            return 0
        return model_call(
            state, segment, lambda can_spend: architecture_model.assess(segment.model, state, can_spend),
            lambda outcome: state.__setitem__("impact", outcome.value),
            "IMPACT_MODEL_FALLBACK", "O impacto veio das regras determinísticas, não do modelo.")

    run_agent(state, segment, ARCHITECTURE, AGENT_VERSION, work)
    return state


def _deterministic_impact(state: dict) -> dict:
    areas: list[str] = []
    for stack in state["stacks"]:
        area = AREA_BY_STACK.get(stack)
        if area and area not in areas:
            areas.append(area)
    if not areas:
        areas.append("projeto")
    risks: list[str] = []
    if not state["evidence"]:
        risks.append("sem-evidencia")
    if not state["stacks"]:
        risks.append("sem-stack")
    refs: list[str] = []
    for item in state["evidence"]:
        chunk = item.get("chunkId") if isinstance(item, dict) else None
        if isinstance(chunk, str) and chunk and chunk not in refs:
            refs.append(chunk)
        if len(refs) == 8:
            break
    return {"schema": "engineering.impact.v1", "areas": areas, "risks": risks, "evidence": refs}


def impact_eval(state: dict) -> dict:
    if impact_ok(state.get("impact"), state.get("evidence")):
        return state
    state["impact"] = None
    state["agents"] = [item for item in state.get("agents") or [] if item.get("agentId") != ARCHITECTURE]
    limit(state, "IMPACT_INVALID", "O impacto técnico não respeita o contrato.")
    return state


def propose_actions(state: dict) -> dict:
    """
    Proposta, nunca execução: só com ``file.write`` permitido pela política, pausa para aprovação e impacto já validado. O
    agente propõe só a intenção; o motor escolhe o alvo, escreve o conteúdo, aprova e executa.
    """
    state["actions"] = []
    if (ACTION_FILE_WRITE in (state.get("allowed_actions") or []) and state["requires_approval"]
            and impact_ok(state.get("impact"), state.get("evidence"))):
        state["actions"] = [decision_intent(state["impact"])]
    return state


def approval_gate(state: dict, segment: Segment) -> dict:
    if not state["requires_approval"] or state.get("approved"):
        return state
    decision = interrupt({
        "interruptId": interrupt_id(state["execution_id"]),
        "actionIds": [action["id"] for action in state.get("actions") or []],
        "eventSeq": segment.emitter.next_seq,
    })
    state["approved"] = isinstance(decision, dict) and decision.get("approved") is True
    return state


def synthesizer(state: dict) -> dict:
    count = len(state["plan"]["steps"]) if isinstance(state.get("plan"), dict) else 0
    detected = len(state["stacks"])
    found = len(state["evidence"])
    # O workflow não grava no projeto; o que muda vem das ações aprovadas, gravadas pelo motor depois deste resumo.
    state["summary"] = f"{count} etapa(s). Stacks detectadas: {detected}. Trechos: {found}."
    return state


def evaluation_judge(state: dict, segment: Segment | None = None) -> dict:
    """
    O veredito é sempre determinístico. A opinião do juiz não volta para o plano nem muda o veredito e, se falhar, só deixa
    uma limitação.
    """
    state["evaluation"] = dict(EVALUATION)
    state["judgment"] = None
    judged = segment is not None and segment.model is not None and _judge_with_model(state, segment)
    if not judged:
        limit(state, "JUDGE_NOT_RUN", "O juiz por modelo não é o veredito desta fase.")
    return state


def _judge_with_model(state: dict, segment: Segment) -> bool:
    segment.emitter.emit("eval.started", {"evalId": JUDGE_EVAL_ID, "kind": "model"})

    def adopt(outcome: Outcome) -> None:
        state["judgment"] = {**outcome.value, "model": outcome.model, "modelVersion": outcome.model_version}

    def call(can_spend: CanSpend) -> Outcome:
        return judge_model.judge(segment.model, state, can_spend)

    tokens = model_call(state, segment, call, adopt, "JUDGE_NOT_RUN", "O juiz por modelo não rodou; o veredito segue determinístico.")
    judgment = state.get("judgment")
    verdict = judgment_outcome(judgment["scores"]) if judgment else "not_run"
    state["evaluation"] = {**EVALUATION, "judge": verdict, "tokens": tokens}
    data = {"evalId": JUDGE_EVAL_ID, "kind": "model", "outcome": verdict}
    if judgment:
        data["criteriaVersion"] = CRITERIA_VERSION
    segment.emitter.emit("eval.completed", data)
    return judgment is not None


def interrupt_id(execution_id: str) -> str:
    return "int-" + hashlib.sha256(f"{execution_id}:approval".encode("utf-8")).hexdigest()[:12]
