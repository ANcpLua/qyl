#!/usr/bin/env python3
from __future__ import annotations

import json
import sys
from pathlib import Path


SKIP_DIRS = {".git", "node_modules", "bin", "obj", "artifacts", "dist", "coverage"}


def wanted(path: Path) -> bool:
    return not any(part in SKIP_DIRS for part in path.parts)


def main() -> int:
    if len(sys.argv) != 2:
        print("usage: classify-architecture.py <repo-path>", file=sys.stderr)
        return 2
    repo = Path(sys.argv[1])
    csprojs = [p for p in repo.rglob("*.csproj") if wanted(p)] if repo.exists() else []
    packages = [p for p in repo.rglob("package.json") if wanted(p)] if repo.exists() else []
    compose = [p for p in (list(repo.glob("compose.y*ml")) + list(repo.rglob("docker-compose*.yml"))) if wanted(p)]
    metrics = [p for p in repo.rglob("*.Metrics.xml") if wanted(p)]
    service_dirs = [p for p in repo.rglob("services/*") if p.is_dir() and wanted(p)] if repo.exists() else []
    src_projects = [p for p in repo.rglob("src/*") if p.is_dir() and wanted(p)] if repo.exists() else []

    score = len(csprojs) + len(packages) + len(service_dirs) + len(src_projects)
    if len(service_dirs) >= 2 or len(compose) > 0:
        label = "service-oriented-or-distributed"
        confidence = 0.72
    elif score >= 4:
        label = "modular-monolith-or-multi-project"
        confidence = 0.66
    elif score >= 1:
        label = "single-project-or-small-system"
        confidence = 0.61
    else:
        label = "unclassified"
        confidence = 0.0

    result = {
        "label": label,
        "confidence": confidence,
        "method": "deterministic repository-structure heuristic",
        "correctness_claim": "none; no ground truth label configured",
        "features": {
            "csproj_count": len(csprojs),
            "package_json_count": len(packages),
            "compose_file_count": len(compose),
            "metrics_xml_count": len(metrics),
            "service_dir_count": len(service_dirs),
            "src_project_dir_count": len(src_projects),
        },
    }
    print(json.dumps(result, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
