#!/usr/bin/env python3
from __future__ import annotations

import json
import sys
from pathlib import Path


ROOT = Path(__file__).resolve().parents[3]


def esc(value: object) -> str:
    text = str(value)
    return text.replace("_", "\\_").replace("%", "\\%").replace("&", "\\&")


def status(step: dict, key: str = "status") -> str:
    return str(step.get(key, "unavailable"))


def main() -> int:
    if len(sys.argv) != 2:
        print("usage: render-thesis-tables.py <run-dir>", file=sys.stderr)
        return 2
    run_dir = Path(sys.argv[1])
    aggregate = json.loads((run_dir / "aggregate-result.json").read_text(encoding="utf-8"))
    results = aggregate["results"]
    tables = ROOT / "research/thesis/tables"
    figures = ROOT / "research/thesis/figures"
    appendix = ROOT / "research/thesis/appendix"
    tables.mkdir(parents=True, exist_ok=True)
    figures.mkdir(parents=True, exist_ok=True)
    appendix.mkdir(parents=True, exist_ok=True)

    rows = ["\\begin{tabular}{llll}", "\\hline", "Target & Build & Test & Coverage \\\\", "\\hline"]
    for result in results:
        cov = result.get("coverage", {})
        cov_text = cov.get("coverage_status")
        if cov_text == "available":
            cov_text = f"{cov.get('line_percent')}\\%"
        rows.append(f"{esc(result['target_id'])} & {esc(status(result['build']))} & {esc(status(result['test']))} & {esc(cov_text)} \\\\")
    rows.extend(["\\hline", "\\end{tabular}"])
    (tables / "evaluation-results.tex").write_text("\n".join(rows) + "\n", encoding="utf-8")

    corpus_rows = ["\\begin{tabular}{lll}", "\\hline", "Target & Commit & Dirty status \\\\", "\\hline"]
    for result in results:
        dirty = "clean" if not result.get("git", {}).get("status_short") else "dirty"
        corpus_rows.append(f"{esc(result['target_id'])} & {esc(result.get('git', {}).get('commit', '')[:10])} & {dirty} \\\\")
    corpus_rows.extend(["\\hline", "\\end{tabular}"])
    (tables / "corpus-summary.tex").write_text("\n".join(corpus_rows) + "\n", encoding="utf-8")

    repro_rows = ["\\begin{tabular}{lll}", "\\hline", "Target & Label & Claim \\\\", "\\hline"]
    for result in results:
        cls = result.get("classification", {})
        repro_rows.append(f"{esc(result['target_id'])} & {esc(cls.get('label'))} & agreement only \\\\")
    repro_rows.extend(["\\hline", "\\end{tabular}"])
    (tables / "reproducibility-results.tex").write_text("\n".join(repro_rows) + "\n", encoding="utf-8")

    cov_rows = ["\\begin{tabular}{lll}", "\\hline", "Target & Coverage status & Line coverage \\\\", "\\hline"]
    for result in results:
        cov = result.get("coverage", {})
        cov_rows.append(f"{esc(result['target_id'])} & {esc(cov.get('coverage_status'))} & {esc(cov.get('line_percent', 'n/a'))} \\\\")
    cov_rows.extend(["\\hline", "\\end{tabular}"])
    (tables / "coverage-results.tex").write_text("\n".join(cov_rows) + "\n", encoding="utf-8")

    mmd = """flowchart LR
  A[Target repository] --> B[Restore build test]
  B --> C[Coverage collector]
  B --> D[Runtime telemetry check]
  B --> E[Arqio classification heuristic]
  C --> F[Aggregate result JSON]
  D --> F
  E --> F
  F --> G[Rego rubric input]
  F --> H[Thesis tables]
  H --> I[thesis.pdf]
"""
    (figures / "evaluation-pipeline.mmd").write_text(mmd, encoding="utf-8")

    index = ["\\section{Raw Evidence Index}"]
    for path in sorted(run_dir.glob("**/*")):
        if path.is_file():
            index.append(f"\\noindent \\texttt{{{esc(path)}}}\\\\")
    (appendix / "raw-evidence-index.tex").write_text("\n".join(index) + "\n", encoding="utf-8")
    print(f"wrote thesis tables for {run_dir}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
