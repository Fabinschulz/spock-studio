"""Divisão estrutural das fontes em trechos.

Nada de corte por quantidade fixa de caracteres: Markdown por seção (com o caminho de títulos), Python pela AST, C# e
TypeScript/JavaScript por declaração, JSON e YAML por chave, especificações também guardam os ids de requisito. Cada
trecho tem uma âncora estável (``path#tipo:nome``): mudar outra parte do arquivo não troca o id do trecho.
"""

from __future__ import annotations

import ast
import json
import re
import unicodedata
from dataclasses import dataclass, field

MAX_BODY = 1600
MAX_CHUNKS = 80
_SLUG = re.compile(r"[^0-9a-zA-Z_.\-]+")
_IDS = re.compile(r"\b(?:REQ|AC|ADR|NFR|RF|RNF|US)-\d{1,4}\b")
_HEADING = re.compile(r"^(#{1,6})\s+(.+?)\s*#*\s*$")
# A declaração começa a linha (depois de atributos e modificadores): "class X" dentro de uma string não conta.
_CS_TYPE = re.compile(
    r"^\s*(?:\[[^\]]*\]\s*)*(?:(?:public|private|protected|internal|static|sealed|abstract|partial|readonly|file|ref|unsafe)\s+)*"
    r"(?:class|record(?:\s+struct|\s+class)?|interface|struct|enum)\s+([A-Za-z_]\w*)"
)
_CS_MEMBER = re.compile(
    r"^\s*(?:\[[^\]]*\]\s*)*(?:(?:public|private|protected|internal|static|async|virtual|override|abstract|sealed|partial|readonly|new)\s+)+"
    r"[\w<>\[\],.?\s]*?\b([A-Za-z_]\w*)\s*(?:<[^>]*>)?\s*\("
)
_CS_NAMESPACE = re.compile(r"^\s*namespace\s+([\w.]+)")
_TS_DECL = re.compile(
    r"^(?:export\s+)?(?:default\s+)?(?:declare\s+)?(?:async\s+)?"
    r"(?:function\*?\s+([A-Za-z_$][\w$]*)|class\s+([A-Za-z_$][\w$]*)|interface\s+([A-Za-z_$][\w$]*)|type\s+([A-Za-z_$][\w$]*)\s*[=<]"
    r"|(?:const|let|var)\s+([A-Za-z_$][\w$]*)\s*(?::[^=]+)?=)"
)
_KEYWORDS = {"if", "for", "foreach", "while", "switch", "catch", "using", "return", "lock", "nameof", "typeof", "new", "base", "this"}
_LANGUAGE = {
    "md": "markdown", "markdown": "markdown", "py": "python", "cs": "csharp", "ts": "typescript", "tsx": "typescript",
    "js": "javascript", "jsx": "javascript", "json": "json", "yml": "yaml", "yaml": "yaml", "java": "java", "kt": "kotlin",
}


@dataclass
class Chunk:
    anchor: str
    kind: str
    body: str
    heading: str = ""
    symbols: list[str] = field(default_factory=list)
    start_line: int = 1


def language_of(path: str) -> str:
    return _LANGUAGE.get(path.rsplit(".", 1)[-1].lower() if "." in path else "", "text")


def chunk(path: str, text: str) -> list[Chunk]:
    language = language_of(path)
    if language == "markdown":
        pieces = _markdown(text)
    elif language == "python":
        pieces = _python(text)
    elif language in {"csharp", "java", "kotlin"}:
        pieces = _declarations(text, _csharp_name)
    elif language in {"typescript", "javascript"}:
        pieces = _declarations(text, _ts_name)
    elif language == "json":
        pieces = _json(text)
    elif language == "yaml":
        pieces = _yaml(text)
    else:
        pieces = []
    if not pieces:
        pieces = _paragraphs(text)
    return _finish(pieces)


def _finish(pieces: list[Chunk]) -> list[Chunk]:
    result: list[Chunk] = []
    seen: dict[str, int] = {}
    for piece in pieces:
        for part, body in enumerate(_split(piece.body)):
            if not body.strip():
                continue
            anchor = piece.anchor if part == 0 else f"{piece.anchor}~{part + 1}"
            count = seen.get(anchor, 0)
            seen[anchor] = count + 1
            if count:
                anchor = f"{anchor}~{count + 1}"
            symbols = list(dict.fromkeys(piece.symbols + _IDS.findall(body)))
            result.append(Chunk(anchor, piece.kind, body, piece.heading, symbols, piece.start_line))
            if len(result) == MAX_CHUNKS:
                return result
    return result


def _split(body: str) -> list[str]:
    body = body.strip("\n")
    if len(body) <= MAX_BODY:
        return [body]
    parts, current = [], ""
    for block in re.split(r"\n\s*\n", body):
        if current and len(current) + len(block) + 2 > MAX_BODY:
            parts.append(current)
            current = ""
        current = f"{current}\n\n{block}" if current else block
        while len(current) > MAX_BODY:
            parts.append(current[:MAX_BODY])
            current = current[MAX_BODY:]
    if current:
        parts.append(current)
    return parts


def _slug(value: str) -> str:
    plain = unicodedata.normalize("NFKD", value).encode("ascii", "ignore").decode("ascii")
    return _SLUG.sub("-", plain.strip()).strip("-").lower()[:80] or "x"


def _markdown(text: str) -> list[Chunk]:
    lines = text.split("\n")
    trail: list[tuple[int, str]] = []
    sections: list[Chunk] = []
    start, heading_path, anchor = 0, "", "intro"
    for number, line in enumerate(lines):
        match = _HEADING.match(line)
        if not match:
            continue
        if number > start:
            sections.append(_section(lines[start:number], heading_path, anchor, start))
        level, title = len(match.group(1)), match.group(2)
        trail = [item for item in trail if item[0] < level] + [(level, title)]
        heading_path = " > ".join(item[1] for item in trail)
        anchor = "h:" + _slug(title)
        start = number
    sections.append(_section(lines[start:], heading_path, anchor, start))
    return [section for section in sections if section.body.strip()]


def _section(lines: list[str], heading: str, anchor: str, start: int) -> Chunk:
    body = "\n".join(lines).strip()
    if " > " in heading:
        # O caminho de títulos dá contexto ao trecho de uma subseção (o título sozinho às vezes não basta).
        body = f"{heading}\n{body}"
    return Chunk(anchor, "section", body, heading, [], start + 1)


def _python(text: str) -> list[Chunk]:
    try:
        tree = ast.parse(text)
    except SyntaxError:
        return []
    lines = text.split("\n")
    pieces: list[Chunk] = []
    for node in tree.body:
        if not isinstance(node, (ast.FunctionDef, ast.AsyncFunctionDef, ast.ClassDef)):
            continue
        first = min([node.lineno] + [decorator.lineno for decorator in node.decorator_list])
        body = "\n".join(lines[first - 1:node.end_lineno])
        symbols = [node.name]
        kind = "class" if isinstance(node, ast.ClassDef) else "function"
        if isinstance(node, ast.ClassDef):
            symbols += [f"{node.name}.{item.name}" for item in node.body if isinstance(item, (ast.FunctionDef, ast.AsyncFunctionDef))]
        pieces.append(Chunk(f"{kind}:{node.name}", kind, body, node.name, symbols, first))
    return pieces


def _csharp_name(line: str) -> tuple[str, str] | None:
    match = _CS_TYPE.match(line)
    if match:
        return "type", match.group(1)
    match = _CS_MEMBER.match(line)
    if match and match.group(1) not in _KEYWORDS:
        return "member", match.group(1)
    return None


def _ts_name(line: str) -> tuple[str, str] | None:
    match = _TS_DECL.match(line)
    if not match:
        return None
    # Só declarações de topo (coluna zero): o que está dentro de uma função não é símbolo do módulo.
    return "toplevel", next(group for group in match.groups() if group)


def _declarations(text: str, name_of) -> list[Chunk]:
    """Um trecho por declaração. Comentários de documentação e atributos logo acima vão junto com a declaração."""
    lines = text.split("\n")
    namespace = ""
    marks: list[tuple[int, str, str]] = []
    for number, line in enumerate(lines):
        namespace_match = _CS_NAMESPACE.match(line)
        if namespace_match:
            namespace = namespace_match.group(1)
            continue
        found = name_of(line)
        if found:
            marks.append((number, found[0], found[1]))
    if not marks:
        return []

    pieces: list[Chunk] = []
    starts = [_doc_start(lines, number) for number, _, _ in marks]
    if starts[0] > 0:
        header = "\n".join(lines[: starts[0]])
        if header.strip():
            pieces.append(Chunk("header", "header", header, namespace, [namespace] if namespace else [], 1))
    owner = ""
    for index, (number, kind, name) in enumerate(marks):
        end = starts[index + 1] if index + 1 < len(marks) else len(lines)
        body = "\n".join(lines[starts[index]:end])
        if kind == "toplevel":
            pieces.append(Chunk(f"sym:{name}", "member", body, name, [name], starts[index] + 1))
        elif kind == "type":
            owner = name
            symbols = [name] + ([f"{namespace}.{name}"] if namespace else [])
            pieces.append(Chunk(f"type:{name}", "type", body, name, symbols, starts[index] + 1))
        else:
            qualified = f"{owner}.{name}" if owner else name
            pieces.append(Chunk(f"sym:{qualified}", "member", body, qualified, [name, qualified], starts[index] + 1))
    return pieces


def _doc_start(lines: list[str], number: int) -> int:
    start = number
    while start > 0:
        previous = lines[start - 1].strip()
        if previous.startswith(("///", "/**", "*", "*/", "[", "@", "//")):
            start -= 1
            continue
        break
    return start


def _json(text: str) -> list[Chunk]:
    try:
        value = json.loads(text)
    except json.JSONDecodeError:
        return []
    if not isinstance(value, dict):
        return []
    pieces = []
    for key, item in value.items():
        body = json.dumps({key: item}, ensure_ascii=False, indent=1)
        symbols = [str(key)] + ([f"{key}.{inner}" for inner in item] if isinstance(item, dict) else [])
        pieces.append(Chunk(f"key:{_slug(str(key))}", "key", body, str(key), symbols, 1))
    return pieces


def _yaml(text: str) -> list[Chunk]:
    lines = text.split("\n")
    blocks: list[tuple[int, str]] = [(number, line.split(":", 1)[0].strip()) for number, line in enumerate(lines)
                                     if line and not line[0].isspace() and not line.startswith("#") and ":" in line]
    if not blocks:
        return []
    pieces: list[Chunk] = []
    for index, (number, key) in enumerate(blocks):
        end = blocks[index + 1][0] if index + 1 < len(blocks) else len(lines)
        block = lines[number:end]
        children = [(offset, line.strip().split(":", 1)[0]) for offset, line in enumerate(block)
                    if re.match(r"^ {2}[^\s#-][^:]*:", line)]
        if len("\n".join(block)) > 400 and len(children) > 1:
            for child_index, (offset, child) in enumerate(children):
                child_end = children[child_index + 1][0] if child_index + 1 < len(children) else len(block)
                body = f"{key}:\n" + "\n".join(block[offset:child_end])
                pieces.append(Chunk(f"key:{_slug(key)}.{_slug(child)}", "key", body, f"{key}.{child}", [child, f"{key}.{child}"], number + offset + 1))
        else:
            pieces.append(Chunk(f"key:{_slug(key)}", "key", "\n".join(block), key, [key] + [child for _, child in children], number + 1))
    return pieces


def _paragraphs(text: str) -> list[Chunk]:
    pieces, start = [], 0
    for number, block in enumerate(re.split(r"\n\s*\n", text)):
        if block.strip():
            pieces.append(Chunk(f"p:{number + 1}", "paragraph", block, "", [], start + 1))
        start += block.count("\n") + 2
    return pieces
