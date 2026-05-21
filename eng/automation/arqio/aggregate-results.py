#!/usr/bin/env python3
from __future__ import annotations

import json
import sys
from collections import defaultdict
from pathlib import Path


def ok(step: dict) -> bool:
    return step.get("status") == "passed"


def pct(n: int, d: int) -> float:
    return round((n / d) * 100.0, 2) if d else 0.0


def main() -> int:
    if len(sys.argv) != 2:
        print("usage: aggregate-results.py <run-dir>", file=sys.stderr)
        return 2
    run_dir = Path(sys.argv[1])
    result_paths = sorted(run_dir.glob("*/*/agent-result.json"))
    results = [json.loads(path.read_text(encoding="utf-8")) for path in result_paths]
    targets = defaultdict(list)
    for result in results:
        targets[result["target_id"]].append(result)

    build_ok = sum(1 for r in results if ok(r.get("build", {})))
    test_ok = sum(1 for r in results if ok(r.get("test", {})))
    coverage_available = sum(1 for r in results if r.get("coverage", {}).get("coverage_status") == "available")
    telemetry_available = sum(1 for r in results if r.get("telemetry", {}).get("status") == "available")
    consistency = {}
    for target_id, target_results in targets.items():
        labels = [r.get("classification", {}).get("label") for r in target_results]
        consistency[target_id] = {
            "labels": labels,
            "label_agreement": len(set(labels)) == 1 if labels else False,
            "claim": "agreement only; correctness requires configured ground truth",
        }

    aggregate = {
        "run_id": run_dir.name,
        "target_count": len(targets),
        "result_count": len(results),
        "build_success_rate": pct(build_ok, len(results)),
        "test_success_rate": pct(test_ok, len(results)),
        "coverage_available_count": coverage_available,
        "telemetry_available_count": telemetry_available,
        "classification_consistency": consistency,
        "results": results,
    }
    (run_dir / "aggregate-result.json").write_text(json.dumps(aggregate, indent=2) + "\n", encoding="utf-8")

    lines = [
        f"# Evaluation Summary: {run_dir.name}",
        "",
        f"- Targets: `{len(targets)}`",
        f"- Results: `{len(results)}`",
        f"- Build success rate: `{aggregate['build_success_rate']}%`",
        f"- Test success rate: `{aggregate['test_success_rate']}%`",
        f"- Coverage available: `{coverage_available}`",
        f"- Telemetry available: `{telemetry_available}`",
        "",
        "Ground-truth classification labels are unset; these results report label agreement and harness feasibility, not classification accuracy.",
    ]
    (run_dir / "summary.md").write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(json.dumps(aggregate, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
