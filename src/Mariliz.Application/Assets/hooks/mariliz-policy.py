#!/usr/bin/env python3
"""
Mariliz policy hook for Claude Code (PreToolUse). Managed by Mariliz AI Engineering.

Deterministic and read-only: standard library only, no network, no writes, never starts another agent, so it
cannot loop or change the repository. It reads the hook payload from stdin and answers with the documented
`hookSpecificOutput.permissionDecision` JSON ("deny" or "ask"); no output means "no objection".

What it enforces:
  - writes outside the project folder are denied;
  - writes to files that normally hold secrets (.env, keys, certificates) are denied;
  - writes to the harness files (.claude/, mariliz.yaml, mariliz.lock, .mcp.json) ask for confirmation, except the
    proposal file .mariliz/proposal.json and TLC feature files under .specs/;
  - catastrophic shell commands are denied; risky ones (force push, hard reset, reading .env) ask.

This is a guard rail, not a security boundary: it matches command text. If this script fails internally it exits
non-zero without blocking (documented hook behavior), and the Mariliz validation reports missing protection.
"""

import json
import os
import re
import sys

WRITE_TOOLS = {"Write", "Edit", "MultiEdit", "NotebookEdit"}
PROTECTED_PREFIXES = (".claude/", ".agents/")
PROTECTED_FILES = ("mariliz.yaml", "mariliz.lock", ".mcp.json")
ALLOWED_WRITES = (".mariliz/proposal.json", ".specs/")

SECRET_FILES = [
    re.compile(r"(^|/)\.env(\.(?!example$|sample$|template$)[^/]+)?$", re.IGNORECASE),
    re.compile(r"\.(pem|pfx|p12|key)$", re.IGNORECASE),
    re.compile(r"(^|/)id_(rsa|dsa|ecdsa|ed25519)$", re.IGNORECASE),
    re.compile(r"(^|/)(credentials|secrets?)\.(json|ya?ml|toml)$", re.IGNORECASE),
]

DENY_COMMANDS = [
    (re.compile(r"\brm\s+(-[a-z]*r[a-z]*\s+-[a-z]*f|-[a-z]*f[a-z]*\s+-[a-z]*r|-[a-z]*(rf|fr)[a-z]*)\s+(--no-preserve-root\s+)?(/|~|\$HOME|\*|\.)(\s|$|;|&|\|)", re.IGNORECASE),
     "recursive delete of a root, home or working directory"),
    (re.compile(r"\b(curl|wget)\b[^|;&]*\|\s*(sudo\s+)?(ba|z|da)?sh\b", re.IGNORECASE), "piping a download into a shell"),
    (re.compile(r"\b(curl|wget)\b[^|;&]*\|\s*(sudo\s+)?python3?\b", re.IGNORECASE), "piping a download into an interpreter"),
    (re.compile(r"\bmkfs(\.\w+)?\b", re.IGNORECASE), "formatting a filesystem"),
    (re.compile(r"\bdd\b[^;&|]*\bof=/dev/", re.IGNORECASE), "writing directly to a device"),
    (re.compile(r"\bchmod\s+-R\s+0?777\s+/", re.IGNORECASE), "recursive world-writable permissions on /"),
    (re.compile(r":\s*\(\s*\)\s*\{\s*:\s*\|\s*:\s*&\s*\}\s*;\s*:"), "fork bomb"),
    (re.compile(r"\bgit\s+push\b[^;&|]*(--force\b|--force-with-lease\b|\s-f\b)[^;&|]*\b(main|master)\b", re.IGNORECASE), "force push to main or master"),
]

ASK_COMMANDS = [
    (re.compile(r"\bgit\s+push\b[^;&|]*(--force\b|--force-with-lease\b|\s-f\b)", re.IGNORECASE), "force push"),
    (re.compile(r"\bgit\s+reset\s+--hard\b", re.IGNORECASE), "git reset --hard discards local work"),
    (re.compile(r"\bgit\s+clean\b[^;&|]*-[a-z]*f", re.IGNORECASE), "git clean deletes untracked files"),
    (re.compile(r"\b(cat|type|more|less|head|tail|grep|rg|sed|awk)\b[^;&|]*\.env(\.(?!example\b|sample\b|template\b)\w+)?(\s|$|\"|')", re.IGNORECASE), "reading an environment file that may hold secrets"),
]


def answer(decision, reason):
    print(json.dumps({
        "hookSpecificOutput": {
            "hookEventName": "PreToolUse",
            "permissionDecision": decision,
            "permissionDecisionReason": "Mariliz policy: " + reason,
        }
    }))
    sys.exit(0)


def project_root(payload):
    return os.path.realpath(os.environ.get("CLAUDE_PROJECT_DIR") or payload.get("cwd") or os.getcwd())


def relative_to_root(path, root, cwd):
    candidate = path if os.path.isabs(path) else os.path.join(cwd, path)
    resolved = os.path.realpath(candidate)
    if os.path.normcase(resolved) != os.path.normcase(root) and not os.path.normcase(resolved).startswith(os.path.normcase(root) + os.sep):
        return None
    return os.path.relpath(resolved, root).replace(os.sep, "/")


def check_write(payload):
    tool_input = payload.get("tool_input") or {}
    target = tool_input.get("file_path") or tool_input.get("notebook_path")
    if not target:
        return
    root = project_root(payload)
    rel = relative_to_root(target, root, payload.get("cwd") or root)
    if rel is None:
        answer("deny", "writing outside the project folder is not allowed.")
    if any(p.search(rel) for p in SECRET_FILES):
        answer("deny", "writing to a file that normally holds secrets is not allowed (" + os.path.basename(rel) + ").")
    if any(rel == a or rel.startswith(a) for a in ALLOWED_WRITES):
        return
    if rel in PROTECTED_FILES or any(rel.startswith(p) for p in PROTECTED_PREFIXES):
        answer("ask", "this file belongs to the managed harness. Change it through Mariliz so it stays reviewable and reversible.")


def check_bash(payload):
    command = (payload.get("tool_input") or {}).get("command") or ""
    for pattern, reason in DENY_COMMANDS:
        if pattern.search(command):
            answer("deny", reason + ".")
    for pattern, reason in ASK_COMMANDS:
        if pattern.search(command):
            answer("ask", reason + ".")


def main():
    try:
        payload = json.load(sys.stdin)
    except (ValueError, OSError):
        sys.exit(1)
    if payload.get("hook_event_name") not in (None, "PreToolUse"):
        return
    tool = payload.get("tool_name")
    if tool in WRITE_TOOLS:
        check_write(payload)
    elif tool == "Bash":
        check_bash(payload)


if __name__ == "__main__":
    main()
