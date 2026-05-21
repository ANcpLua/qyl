#!/usr/bin/env python3
from __future__ import annotations

import json
import sys
from pathlib import Path


def main() -> int:
    if len(sys.argv) != 3:
        print("usage: semconv-summary.py <artifact-dir> <out-json>", file=sys.stderr)
        return 2
    artifact_dir = Path(sys.argv[1])
    out_json = Path(sys.argv[2])
    logs = sorted(artifact_dir.glob("*.log"))
    steps = {}
    for log in logs:
        exit_file = log.with_suffix(".exit")
        exit_code = int(exit_file.read_text().strip()) if exit_file.exists() else 999
        steps[log.stem] = {"exit_code": exit_code, "status": "passed" if exit_code == 0 else "failed", "log": str(log)}
    summary = {
        "status": "passed" if steps and all(step["exit_code"] == 0 for step in steps.values()) else "failed",
        "claim": "local semconv build/test verification only; no package publishing performed",
        "steps": steps,
    }
    out_json.parent.mkdir(parents=True, exist_ok=True)
    out_json.write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")
    md = out_json.with_suffix(".md")
    lines = ["# Semantic Convention Verification", "", f"- Status: `{summary['status']}`", f"- Claim: {summary['claim']}"]
    for name, step in steps.items():
        lines.append(f"- {name}: `{step['status']}` (`{step['log']}`)")
    md.write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(json.dumps(summary, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
