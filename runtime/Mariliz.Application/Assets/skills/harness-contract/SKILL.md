---
name: harness-contract
description: How agents in this repository state what they know. Source-of-truth hierarchy (what the system does today versus what it should do), claim classification (FACT, INFERENCE, ASSUMPTION, UNKNOWN), confidence scale and the structured block that opens an agent's output. Use when an agent must declare confidence, cite evidence, resolve a conflict between sources, or decide whether it has enough evidence to act. Not a source of code conventions.
---

# Harness contract

> Adapted from the Harness Engineering patterns in github.com/Fabinschulz/harness-engineering (MIT).

Rules for how work flows between agents: what counts as truth, what counts as evidence, and how confidence is
declared. One definition, read by every agent.

## Source of truth

Two different questions have two different hierarchies. Mixing them is the usual way to reach a wrong conclusion
with high confidence.

**Axis A, "what does the system do today?"** (debugging, QA, diagnosis)

1. Observed execution: command output, log, real response, navigated screen.
2. Current code.
3. Tests that pass today.
4. Documentation. It describes intent and does not guarantee behavior.

**Axis B, "what should the system do?"** (design, implementation, conformance review)

1. Recorded structural decisions (ADRs, `.specs/STATE.md`).
2. The spec and design of the active feature.
3. Project conventions (rules and skills).
4. Governance documents.
5. Memory from previous sessions: history, not norm.
6. The agent's own inference: always last, always labeled.

When the axes diverge (the code does X, a decision says Y), that is a finding. Report it, say which axis you
follow and why, and hand the decision back when the fix is not obvious.

A fact from memory or an old document was true when it was written. Confirm it against Axis A before acting.

## Classifying claims

| Label | Definition | Requires |
| --- | --- | --- |
| FACT | Verified directly in this run | `file:line`, command and output, or an observation |
| INFERENCE | Deduced from facts | The starting facts and the logical step |
| ASSUMPTION | Assumed for lack of information | Stay explicit; never the only support of an irreversible decision |
| UNKNOWN | Not known and not deducible | What is missing and how to obtain it |

Never promote an assumption or an inference to a fact by repetition. Never present UNKNOWN as "no problem found".
Never describe a command as executed when it was not.

## Confidence

| Level | All of these must hold |
| --- | --- |
| HIGH | Primary source read; validation executed with real output; every criterion checked; no contradiction |
| MEDIUM | Partial evidence, or a validation that could not run. The uncertainty is named |
| LOW | Insufficient information, conflicting sources, or an assumption supports the main conclusion |

Confidence is the weakest link, not the average.

## Output block

An agent that judges evidence opens its output with this literal YAML, before the human-readable report:

```yaml
confidence: HIGH | MEDIUM | LOW
evidence:
  - "path/to/file.ext:48 — what was read"
  - "command — result"
assumptions:
  - "none"
open_questions:
  - "none"
blocked_by: null
```

`evidence` points to something verifiable. An empty `assumptions` means no assumption was made, not that none was
checked. Never put a secret in the block or anywhere else in the output.
