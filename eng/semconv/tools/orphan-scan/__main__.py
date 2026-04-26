#!/usr/bin/env python3
# Copyright (c) 2025-2026 ancplua
"""
orphan-scan — find attribute-shaped string literals in qyl C# that do not
correspond to any known OTel semantic-convention id (upstream or qyl-custom).

Run:
    python3 -m orphan_scan --repo-root . --report orphans.json

Registry inputs (each optional, skipped if missing):
    - .tools/semconv-upstream/model/**/*.yaml        (upstream OTel registry)
    - eng/semconv/qyl/model/**/*.yaml                (qyl-custom additions)

Scan inputs:
    services/**/*.cs
    packages/**/*.cs

For each literal matching the attribute-id shape (`xxx.yyy[.zzz]`) used as the
first argument of a tag-setter (SetTag/AddTag/SetAttribute/…), the script
reports the file, line, literal, and up to three Levenshtein suggestions from
the combined registry.
"""
from __future__ import annotations

import argparse
import json
import pathlib
import re
import sys
from dataclasses import dataclass, field

import yaml

TAG_METHODS = (
    "SetTag",
    "AddTag",
    "SetAttribute",
    "AddAttribute",
    "SetCustomProperty",
    "SetBaggage",
)

# Matches invocations like `.SetTag("x.y.z", ...)` — captures the literal text and start column.
_INVOCATION = re.compile(
    r"\.(?:" + "|".join(TAG_METHODS) + r")\s*\(\s*\"([A-Za-z0-9_.\-]+)\"",
)

# Accept only names with at least one dot — pure identifiers are unlikely to be attributes.
_ATTR_SHAPE = re.compile(r"^[A-Za-z][A-Za-z0-9_]*(?:\.[A-Za-z0-9_]+)+$")


@dataclass
class OrphanHit:
    file: str
    line: int
    literal: str
    suggestions: list[dict]


@dataclass
class Registry:
    ids: set[str] = field(default_factory=set)
    sources: list[str] = field(default_factory=list)

    def add_from_yaml(self, path: pathlib.Path) -> int:
        try:
            with path.open("r", encoding="utf-8") as f:
                doc = yaml.safe_load(f)
        except (OSError, yaml.YAMLError):
            return 0
        if not isinstance(doc, dict):
            return 0

        added = 0
        for group in doc.get("groups") or []:
            if not isinstance(group, dict):
                continue
            group_prefix = group.get("prefix") or ""
            for attr in group.get("attributes") or []:
                if not isinstance(attr, dict):
                    continue
                name = attr.get("id") or attr.get("name")
                if not name:
                    continue
                fq = f"{group_prefix}.{name}" if group_prefix and "." not in name else name
                if _ATTR_SHAPE.match(fq):
                    self.ids.add(fq)
                    added += 1
            # Metrics / events with first-class id
            metric_id = group.get("metric_name") or group.get("event_name")
            if metric_id and _ATTR_SHAPE.match(metric_id):
                self.ids.add(metric_id)
                added += 1
        if added:
            self.sources.append(str(path))
        return added


def levenshtein(a: str, b: str, cap: int = 5) -> int:
    if abs(len(a) - len(b)) > cap:
        return cap + 1
    if a == b:
        return 0
    if not a or not b:
        return max(len(a), len(b))
    prev = list(range(len(b) + 1))
    for i, ca in enumerate(a, start=1):
        cur = [i] + [0] * len(b)
        row_best = cur[0]
        for j, cb in enumerate(b, start=1):
            cost = 0 if ca == cb else 1
            cur[j] = min(prev[j] + 1, cur[j - 1] + 1, prev[j - 1] + cost)
            row_best = min(row_best, cur[j])
        if row_best > cap:
            return cap + 1
        prev = cur
    return prev[-1]


def top_suggestions(literal: str, registry: Registry, limit: int = 3, max_distance: int = 4) -> list[dict]:
    prefix = literal.split(".", 1)[0]
    ranked: list[tuple[int, str]] = []
    for candidate in registry.ids:
        # Cheap filter: only rank candidates that share the top-level prefix.
        if not candidate.startswith(prefix + "."):
            continue
        dist = levenshtein(literal, candidate, cap=max_distance)
        if dist <= max_distance:
            ranked.append((dist, candidate))
    ranked.sort()
    return [{"candidate": c, "distance": d} for d, c in ranked[:limit]]


def scan_file(path: pathlib.Path, repo_root: pathlib.Path, registry: Registry) -> list[OrphanHit]:
    hits: list[OrphanHit] = []
    try:
        text = path.read_text(encoding="utf-8", errors="replace")
    except OSError:
        return hits

    for line_no, line in enumerate(text.splitlines(), start=1):
        for m in _INVOCATION.finditer(line):
            literal = m.group(1)
            if not _ATTR_SHAPE.match(literal):
                continue
            if literal in registry.ids:
                continue
            rel = path.relative_to(repo_root).as_posix()
            hits.append(OrphanHit(
                file=rel,
                line=line_no,
                literal=literal,
                suggestions=top_suggestions(literal, registry),
            ))
    return hits


def load_registry(paths: list[pathlib.Path]) -> Registry:
    registry = Registry()
    for root in paths:
        if not root.exists():
            continue
        for yaml_path in root.rglob("*.yaml"):
            registry.add_from_yaml(yaml_path)
    return registry


def discover_cs_files(repo_root: pathlib.Path, scan_dirs: list[str]) -> list[pathlib.Path]:
    out: list[pathlib.Path] = []
    for rel in scan_dirs:
        root = repo_root / rel
        if not root.exists():
            continue
        out.extend(root.rglob("*.cs"))
    return out


def main() -> int:
    parser = argparse.ArgumentParser(prog="orphan-scan", description=__doc__)
    parser.add_argument("--repo-root", default=".", type=pathlib.Path)
    parser.add_argument(
        "--registry-dir",
        action="append",
        default=None,
        help="Additional registry directory to scan for YAML models (repeatable).",
    )
    parser.add_argument(
        "--scan-dir",
        action="append",
        default=None,
        help="Source directory to scan for C# attribute-like literals (repeatable).",
    )
    parser.add_argument("--report", type=pathlib.Path, default=None, help="Output JSON file; omit for stdout.")
    parser.add_argument("--limit", type=int, default=None, help="Cap number of reported orphans.")
    args = parser.parse_args()

    repo_root = args.repo_root.resolve()
    registry_dirs = [
        repo_root / p for p in (
            args.registry_dir
            or [".tools/semconv-upstream/model", "eng/semconv/qyl/model"]
        )
    ]
    scan_dirs = args.scan_dir or ["services", "packages"]

    registry = load_registry(registry_dirs)
    cs_files = discover_cs_files(repo_root, scan_dirs)

    all_hits: list[OrphanHit] = []
    for cs in cs_files:
        all_hits.extend(scan_file(cs, repo_root, registry))

    if args.limit:
        all_hits = all_hits[: args.limit]

    payload = {
        "repo_root": str(repo_root),
        "registry_sources": registry.sources,
        "registry_id_count": len(registry.ids),
        "orphan_count": len(all_hits),
        "orphans": [h.__dict__ for h in all_hits],
    }
    text = json.dumps(payload, indent=2)
    if args.report:
        args.report.write_text(text, encoding="utf-8")
        print(
            f"[orphan-scan] wrote {args.report} — {len(all_hits)} orphans "
            f"from {len(registry.ids)} registered ids",
            file=sys.stderr,
        )
    else:
        sys.stdout.write(text)
        sys.stdout.write("\n")

    return 0


if __name__ == "__main__":
    sys.exit(main())
