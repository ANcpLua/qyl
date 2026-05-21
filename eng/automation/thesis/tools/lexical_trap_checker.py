#!/usr/bin/env python3
"""Small deterministic lexical-trap checker for CSAM thesis drafts."""
from __future__ import annotations

import re
import sys
from pathlib import Path

TRAPS: list[tuple[str, str]] = [
    (r"\bsignificant\b(?!\s*(?:statistically|statistical|p\s*<\s*0\.0[1-5]))", "Use a measured claim or 'substantial'; keep 'statistically significant' only with p-value/test."),
    (r"\b(prove|proved|proves|proving)\b", "Prefer 'indicates', 'demonstrates', or 'supports' unless formal proof is provided."),
    (r"\brandom\b(?!\s*(?:seed|number|distribution|generator|sample))", "Use 'arbitrary' or define the sampling procedure."),
    (r"\bunique\b", "Avoid unless uniqueness is established against an explicit comparison set."),
    (r"\b(very|really|a lot|big)\b", "Replace vague intensity with a measured quantity."),
    (r"\b(obviously|clearly|of course)\b", "Replace with evidence or remove."),
    (r"\bin order to\b", "Use 'to'."),
    (r"\bdue to the fact that\b", "Use 'because'."),
    (r"\bstate of the art\b", "Use only after surveying current research; otherwise use 'current practice' or 'existing work'."),
    (r"\bnovel\b", "Use only after an explicit novelty comparison."),
    (r"\b(scalable|robust|efficient|performant)\b(?!\s*(?:up to|to|by|with|under|for|against))", "Attach quality claim to a condition or metric."),
    (r"\bdata shows\b", "Use 'the data indicate' or 'the measurements suggest'."),
]


def strip_tex_or_markdown(text: str) -> str:
    text = re.sub(r"```.*?```", " ", text, flags=re.S)
    text = re.sub(r"\\begin\{(?:lstlisting|verbatim|minted)\}.*?\\end\{(?:lstlisting|verbatim|minted)\}", " ", text, flags=re.S)
    text = re.sub(r"(?<!\\)%.*", "", text)
    text = re.sub(r"\\[a-zA-Z]+\*?(?:\[[^]]*\])?(?:\{([^{}]*)\})?", lambda m: m.group(1) or " ", text)
    return text


def line_col(text: str, index: int) -> tuple[int, int]:
    line = text.count("\n", 0, index) + 1
    last = text.rfind("\n", 0, index)
    col = index + 1 if last < 0 else index - last
    return line, col


def main(argv: list[str]) -> int:
    if len(argv) != 2:
        print("Usage: lexical_trap_checker.py thesis.tex|thesis.md|thesis.txt", file=sys.stderr)
        return 2
    path = Path(argv[1])
    if not path.exists():
        print(f"Error: file not found: {path}", file=sys.stderr)
        return 2
    raw = path.read_text(encoding="utf-8", errors="replace")
    text = strip_tex_or_markdown(raw)
    findings: list[str] = []
    for pattern, advice in TRAPS:
        for m in re.finditer(pattern, text, re.I):
            line, col = line_col(text, m.start())
            ctx = re.sub(r"\s+", " ", text[max(0, m.start()-70):m.end()+90]).strip()
            findings.append(f"{path}:{line}:{col}: warning lexical trap '{m.group(0)}' — {advice}\n    context: {ctx}")
    if findings:
        print("\n".join(findings))
        print(f"\n{len(findings)} lexical trap(s) found.")
        return 1
    print("No lexical traps found.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv))
