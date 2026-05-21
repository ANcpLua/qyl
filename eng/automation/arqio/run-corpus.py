#!/usr/bin/env python3
from __future__ import annotations

import json
import os
import shutil
import subprocess
import sys
import time
from datetime import datetime, timezone
from pathlib import Path


ROOT = Path(__file__).resolve().parents[3]
CLASSIFIER = ROOT / "eng/automation/arqio/classify-architecture.py"
COVERAGE = ROOT / "eng/automation/coverage/run-dotnet-coverage.sh"


def now() -> str:
    return datetime.now(timezone.utc).isoformat()


def run(cmd: list[str], cwd: Path, log: Path, env: dict[str, str] | None = None, timeout: int = 900) -> dict:
    started = time.monotonic()
    merged_env = os.environ.copy()
    if env:
        merged_env.update(env)
    with log.open("w", encoding="utf-8") as stream:
        stream.write("$ " + " ".join(cmd) + "\n")
        try:
            proc = subprocess.run(cmd, cwd=cwd, env=merged_env, stdout=stream, stderr=subprocess.STDOUT, timeout=timeout)
            code = proc.returncode
            status = "passed" if code == 0 else "failed"
        except subprocess.TimeoutExpired:
            code = 124
            status = "timeout"
            stream.write(f"\nTIMEOUT after {timeout}s\n")
        except FileNotFoundError as ex:
            code = 127
            status = "unavailable"
            stream.write(f"\nUNAVAILABLE: {ex}\n")
    return {"status": status, "exit_code": code, "duration_seconds": round(time.monotonic() - started, 3), "log": str(log)}


def output(cmd: list[str], cwd: Path) -> str:
    try:
        return subprocess.check_output(cmd, cwd=cwd, stderr=subprocess.STDOUT, text=True).strip()
    except Exception as ex:
        return f"unavailable: {ex}"


def discover_solution(repo: Path, configured: str | None) -> Path | None:
    if configured:
        candidate = repo / configured
        if candidate.exists():
            return candidate
    for pattern in ("*.slnx", "*.sln", "*.csproj"):
        matches = sorted(repo.glob(pattern))
        if matches:
            return matches[0]
    return None


def discover_node_root(repo: Path) -> Path | None:
    if (repo / "package.json").exists():
        return repo
    angular_roots = sorted(p.parent for p in repo.rglob("angular.json") if "node_modules" not in p.parts)
    if angular_roots:
        return angular_roots[0]
    package_roots = sorted(p.parent for p in repo.rglob("package.json") if "node_modules" not in p.parts)
    return package_roots[0] if package_roots else None


def package_scripts(repo: Path) -> dict:
    package = repo / "package.json"
    if not package.exists():
        return {}
    try:
        data = json.loads(package.read_text(encoding="utf-8"))
        return data.get("scripts", {})
    except json.JSONDecodeError:
        return {}


def clone_if_needed(target: dict, target_dir: Path) -> dict | None:
    if target_dir.exists():
        return None
    clone_url = target.get("clone_url")
    if not clone_url:
        return {"status": "missing", "reason": f"path does not exist: {target_dir}"}
    target_dir.parent.mkdir(parents=True, exist_ok=True)
    log = ROOT / "reports/arqio-evaluation" / f"clone-{target['id']}.log"
    result = run(["git", "clone", clone_url, str(target_dir)], target_dir.parent, log, timeout=1200)
    if result["status"] != "passed":
        return {"status": "clone_failed", "clone": result}
    return None


def classify(repo: Path, out_dir: Path) -> dict:
    log = out_dir / "classification.log"
    result = run(["python3", str(CLASSIFIER), str(repo)], ROOT, log)
    try:
        text = log.read_text(encoding="utf-8")
        start = text.find("{")
        end = text.rfind("}")
        return json.loads(text[start:end + 1])
    except Exception:
        return {"label": "unclassified", "confidence": 0.0, "method": "classification failed", "log": str(log)}


def read_json(path: Path, fallback: dict) -> dict:
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except Exception:
        return fallback


def run_target(target: dict, run_id: str, iteration: int, run_dir: Path) -> dict:
    target_id = target["id"]
    repo = Path(target["path"])
    out_dir = run_dir / target_id / f"iteration-{iteration}"
    out_dir.mkdir(parents=True, exist_ok=True)
    clone_problem = clone_if_needed(target, repo)
    if clone_problem:
        result = {
            "target_id": target_id,
            "run_id": run_id,
            "iteration": iteration,
            "started_at": now(),
            "finished_at": now(),
            "git": clone_problem,
            "build": {"status": "unavailable", "reason": "target path unavailable"},
            "test": {"status": "unavailable", "reason": "target path unavailable"},
            "coverage": {"coverage_status": "unavailable", "reason": "target path unavailable"},
            "telemetry": {"status": "unavailable", "reason": "target path unavailable"},
            "classification": {"label": "unclassified", "confidence": 0.0},
            "logs": {},
        }
        (out_dir / "agent-result.json").write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
        return result

    started = now()
    git = {
        "commit": output(["git", "rev-parse", "HEAD"], repo),
        "status_short": output(["git", "status", "--short"], repo),
        "branch": output(["git", "branch", "--show-current"], repo),
    }
    solution = discover_solution(repo, target.get("solution"))
    logs = {}
    build = {"status": "unavailable", "reason": "no supported build file discovered"}
    test = {"status": "unavailable", "reason": "no supported test command discovered"}

    kind = target.get("kind", "")
    if solution and ("dotnet" in kind or solution.suffix in (".sln", ".slnx", ".csproj")):
        logs["restore"] = str(out_dir / "dotnet-restore.log")
        restore = run(["dotnet", "restore", str(solution)], repo, out_dir / "dotnet-restore.log")
        build = run(["dotnet", "build", str(solution), "--no-restore"], repo, out_dir / "dotnet-build.log")
        logs["build"] = build["log"]
        if build["status"] == "passed":
            test = run(["dotnet", "test", str(solution), "--no-build", "--results-directory", str(out_dir / "TestResults")], repo, out_dir / "dotnet-test.log")
        else:
            test = {"status": "skipped", "reason": "build did not pass", "log": str(out_dir / "dotnet-test.log")}
        logs["test"] = test.get("log")
    else:
        node_root = discover_node_root(repo)
        if node_root is None:
            node_root = repo
        scripts = package_scripts(node_root)
        install_cmd = ["npm", "ci"] if (node_root / "package-lock.json").exists() else ["npm", "install"]
        install = run(install_cmd, node_root, out_dir / "npm-install.log", timeout=1200)
        if "build" in scripts:
            build = run(["npm", "run", "build"], node_root, out_dir / "npm-build.log", timeout=1200)
        else:
            build = {"status": "skipped", "reason": "package.json has no build script"}
        if "test" in scripts:
            test = run(["npm", "test", "--", "--watch=false"], node_root, out_dir / "npm-test.log", timeout=1200)
        else:
            test = {"status": "skipped", "reason": "package.json has no test script"}
        logs.update({"install": install["log"], "build": build.get("log"), "test": test.get("log")})

    coverage = {"coverage_status": "not_required"}
    if target.get("coverage_required") and solution and ("dotnet" in kind or solution.suffix in (".sln", ".slnx", ".csproj")):
        coverage_dir = out_dir / "coverage"
        run(["bash", str(COVERAGE), str(solution), str(coverage_dir)], repo, out_dir / "coverage-runner.log", timeout=1800)
        coverage = read_json(coverage_dir / "coverage-summary.json", {"coverage_status": "unavailable", "reason": "coverage summary not written"})
    elif target.get("coverage_required") and (repo / "package.json").exists():
        coverage = {"coverage_status": "unavailable", "reason": "node coverage command not configured in first harness pass"}

    telemetry = {"status": "not_required"}
    if target.get("telemetry_required"):
        compose = ROOT / "eng/compose.yaml"
        if shutil.which("docker") and compose.exists():
            config = run(["docker", "compose", "-f", str(compose), "config"], ROOT, out_dir / "docker-compose-config.log", timeout=120)
            telemetry = {"status": "configured" if config["status"] == "passed" else "unavailable", "docker_compose_config": config}
        else:
            telemetry = {"status": "unavailable", "reason": "docker missing or eng/compose.yaml absent"}

    classification = classify(repo, out_dir)
    result = {
        "target_id": target_id,
        "target_name": target.get("name"),
        "run_id": run_id,
        "iteration": iteration,
        "started_at": started,
        "finished_at": now(),
        "git": git,
        "build": build,
        "test": test,
        "coverage": coverage,
        "telemetry": telemetry,
        "classification": classification,
        "logs": logs,
    }
    (out_dir / "agent-result.json").write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    return result


def main() -> int:
    if len(sys.argv) < 2:
        print("usage: run-corpus.py <targets.json> [run-dir]", file=sys.stderr)
        return 2
    targets_path = Path(sys.argv[1])
    run_id = datetime.now().strftime("%Y%m%d-%H%M%S-local")
    run_dir = Path(sys.argv[2]) if len(sys.argv) > 2 else ROOT / "research/arqio/results" / run_id
    run_dir.mkdir(parents=True, exist_ok=True)
    targets = json.loads(targets_path.read_text(encoding="utf-8"))
    repetitions = int(os.environ.get("CORPUS_REPETITIONS", "1"))
    all_results = []
    started = now()
    for iteration in range(1, repetitions + 1):
        for target in targets:
            all_results.append(run_target(target, run_dir.name, iteration, run_dir))
    run = {"run_id": run_dir.name, "started_at": started, "finished_at": now(), "targets": targets, "results": all_results}
    (run_dir / "evaluation-run.json").write_text(json.dumps(run, indent=2) + "\n", encoding="utf-8")
    print(str(run_dir))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
