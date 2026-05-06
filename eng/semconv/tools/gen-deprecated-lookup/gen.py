#!/usr/bin/env python3
# Copyright (c) 2025-2026 ancplua
"""
Generate eng/semconv/deprecated-lookup/master-programmatic.yaml from the
upstream OTel semantic-conventions registry pinned at .tools/semconv-upstream
(submodule, currently v1.41.0).

Walks model/**/*.{yaml,yml}, extracts every deprecated attribute, metric,
event, entity, and enum-member, and emits a deterministic YAML record per
entry. Records carry the source file path and 1-based line number so a
downstream Roslyn analyzer (gen-deprecated-diagnostics) can deep-link into
the upstream registry.

Replacement-mode classification is rule-based, not LLM-derived:

  * deprecated.renamed_to is a string             -> direct
  * deprecated.renamed_to is a list of length 1   -> direct
  * deprecated.renamed_to is a list of length 2+  -> alternative
  * deprecated.reason == "obsoleted" and the note
    matches /removed.*no\\s+replacement/i         -> removed
  * uncategorized note with backtick-quoted ids:
      0 ids                                       -> note_only
      1 id   + /integrate|included|moved/i        -> integrate
      1 id                                        -> direct
      2+ ids + /\\bor\\b/                         -> alternative
      2+ ids                                      -> composite
  * fallback                                      -> note_only

Output is sorted by (folder, kind, parent_id|"", deprecated_id|deprecated_member_id)
so generation is byte-identical on clean checkouts.

Usage:
    python3 eng/semconv/tools/gen-deprecated-lookup/gen.py \\
        --registry .tools/semconv-upstream/model \\
        --out      eng/semconv/deprecated-lookup/master-programmatic.yaml \\
        --tag      v1.41.0 \\
        --commit   $(git -C .tools/semconv-upstream rev-parse HEAD)
"""
from __future__ import annotations

import argparse
import pathlib
import re
import subprocess
import sys
from collections import Counter
from typing import Any

import yaml

BACKTICK_ID = re.compile(r"`([a-z][a-z0-9_.]*(?:\.[a-z0-9_.]+)*)`")
ID_RE = re.compile(r"^[a-z][a-z0-9_.]*$")
INTEGRATE_RE = re.compile(r"\b(integrate|included|moved into|merged into|included into)\b", re.I)
REMOVED_RE = re.compile(r"removed.*no\s+replacement|no\s+replacement", re.I)
OR_RE = re.compile(r"\bor\b", re.I)


class LineLoader(yaml.SafeLoader):
    """SafeLoader that records 1-based start lines on mapping nodes."""


def _construct_mapping(loader: LineLoader, node: yaml.MappingNode) -> dict:
    mapping = loader.construct_mapping(node, deep=True)
    mapping["__line__"] = node.start_mark.line + 1
    return mapping


LineLoader.add_constructor(yaml.resolver.BaseResolver.DEFAULT_MAPPING_TAG, _construct_mapping)


def load_yaml(path: pathlib.Path) -> Any:
    with path.open("r", encoding="utf-8") as f:
        return yaml.load(f, Loader=LineLoader)


def folder_of(rel_path: pathlib.Path) -> str:
    parts = rel_path.parts
    return parts[0] if parts else ""


def extract_note_targets(note: str | None) -> list[str]:
    if not note:
        return []
    seen: list[str] = []
    for match in BACKTICK_ID.findall(note):
        if ID_RE.match(match) and match not in seen:
            seen.append(match)
    return seen


def classify_replacement(
    *,
    reason: str,
    renamed_to: Any,
    note: str | None,
    note_targets: list[str],
) -> tuple[str, list[str], str, str]:
    """Return (mode, replacements, resolution_text, resolution_basis)."""
    if isinstance(renamed_to, str):
        return "direct", [renamed_to], renamed_to, "deprecated.renamed_to"
    if isinstance(renamed_to, list):
        items = [str(x) for x in renamed_to]
        if len(items) == 1:
            return "direct", items, items[0], "deprecated.renamed_to"
        return "alternative", items, ", ".join(items), "deprecated.renamed_to"

    if reason == "obsoleted" and note and REMOVED_RE.search(note):
        return "removed", [], "removed, no replacement", "deprecated.note"

    if not note_targets:
        if note:
            stripped = " ".join(note.split())
            return "note_only", [], stripped, "deprecated.note"
        return "note_only", [], "", "deprecated.reason"

    if len(note_targets) == 1:
        target = note_targets[0]
        if note and INTEGRATE_RE.search(note):
            return "integrate", [target], f"integrate into {target}", "deprecated.note"
        return "direct", [target], target, "deprecated.note"

    if note and OR_RE.search(note):
        return "alternative", note_targets, ", ".join(note_targets), "deprecated.note"

    return "composite", note_targets, ", ".join(note_targets), "deprecated.note"


def _maybe_normalize_renamed_raw(value: Any) -> Any:
    if isinstance(value, list):
        return list(value)
    return value


def emit_attribute_entry(
    *,
    folder: str,
    rel_path: pathlib.Path,
    attr: dict,
) -> dict | None:
    deprecated = attr.get("deprecated")
    if not deprecated:
        return None
    reason = deprecated.get("reason") or "uncategorized"
    renamed_to = deprecated.get("renamed_to")
    note = deprecated.get("note")
    note_targets = extract_note_targets(note)
    mode, replacements, text, basis = classify_replacement(
        reason=reason, renamed_to=renamed_to, note=note, note_targets=note_targets
    )
    return {
        "folder": folder,
        "kind": "attribute",
        "deprecated_id": attr["id"],
        "status": reason,
        "replacements": replacements,
        "source_file": str(rel_path).replace("\\", "/"),
        "source_line": attr.get("__line__") or deprecated.get("__line__") or 0,
        "source_renamed_to_raw": _maybe_normalize_renamed_raw(renamed_to),
        "source_note_raw": (note.strip() if isinstance(note, str) else None),
        "source_note_targets": note_targets,
        "replacement_mode": mode,
        "resolution_text": text,
        "resolution_basis": basis,
    }


def emit_group_entry(
    *,
    folder: str,
    rel_path: pathlib.Path,
    group: dict,
    kind: str,
    id_field: str,
) -> dict | None:
    deprecated = group.get("deprecated")
    if not deprecated:
        return None
    reason = deprecated.get("reason") or "uncategorized"
    renamed_to = deprecated.get("renamed_to")
    note = deprecated.get("note")
    note_targets = extract_note_targets(note)
    mode, replacements, text, basis = classify_replacement(
        reason=reason, renamed_to=renamed_to, note=note, note_targets=note_targets
    )
    return {
        "folder": folder,
        "kind": kind,
        "deprecated_id": group[id_field],
        "status": reason,
        "replacements": replacements,
        "source_file": str(rel_path).replace("\\", "/"),
        "source_line": group.get("__line__") or 0,
        "source_renamed_to_raw": _maybe_normalize_renamed_raw(renamed_to),
        "source_note_raw": (note.strip() if isinstance(note, str) else None),
        "source_note_targets": note_targets,
        "replacement_mode": mode,
        "resolution_text": text,
        "resolution_basis": basis,
    }


def emit_enum_member_entry(
    *,
    folder: str,
    rel_path: pathlib.Path,
    parent_attr: dict,
    member: dict,
) -> dict | None:
    deprecated = member.get("deprecated")
    if not deprecated:
        return None
    reason = deprecated.get("reason") or "uncategorized"
    renamed_to = deprecated.get("renamed_to")
    note = deprecated.get("note")
    note_targets = extract_note_targets(note)
    mode, replacements, text, basis = classify_replacement(
        reason=reason, renamed_to=renamed_to, note=note, note_targets=note_targets
    )
    return {
        "folder": folder,
        "kind": "enum_member",
        "parent_id": parent_attr["id"],
        "deprecated_member_id": member["id"],
        "status": reason,
        "replacements": replacements,
        "source_file": str(rel_path).replace("\\", "/"),
        "source_line": member.get("__line__") or 0,
        "source_renamed_to_raw": _maybe_normalize_renamed_raw(renamed_to),
        "source_note_raw": (note.strip() if isinstance(note, str) else None),
        "source_note_targets": note_targets,
        "replacement_mode": mode,
        "resolution_text": text,
        "resolution_basis": basis,
    }


def walk_registry(registry: pathlib.Path) -> list[dict]:
    entries: list[dict] = []
    paths: list[pathlib.Path] = []
    for ext in ("*.yaml", "*.yml"):
        paths.extend(registry.rglob(ext))
    paths.sort()

    for path in paths:
        rel = path.relative_to(registry.parent)
        folder = folder_of(path.relative_to(registry))
        try:
            doc = load_yaml(path)
        except yaml.YAMLError as exc:
            print(f"[gen-deprecated-lookup] skip {rel}: {exc}", file=sys.stderr)
            continue
        if not isinstance(doc, dict):
            continue
        groups = doc.get("groups") or []
        for group in groups:
            if not isinstance(group, dict):
                continue
            gtype = group.get("type", "")

            if gtype == "metric":
                entry = emit_group_entry(
                    folder=folder, rel_path=rel, group=group,
                    kind="metric", id_field="metric_name",
                )
                if entry:
                    entries.append(entry)
            elif gtype == "event":
                entry = emit_group_entry(
                    folder=folder, rel_path=rel, group=group,
                    kind="event",
                    id_field="name" if "name" in group else "id",
                )
                if entry:
                    entries.append(entry)
            elif gtype == "entity":
                entry = emit_group_entry(
                    folder=folder, rel_path=rel, group=group,
                    kind="entity", id_field="id",
                )
                if entry:
                    entries.append(entry)

            for attr in (group.get("attributes") or []):
                if not isinstance(attr, dict) or "id" not in attr:
                    continue
                a_entry = emit_attribute_entry(folder=folder, rel_path=rel, attr=attr)
                if a_entry:
                    entries.append(a_entry)
                attr_type = attr.get("type")
                if isinstance(attr_type, dict):
                    for member in (attr_type.get("members") or []):
                        if not isinstance(member, dict) or "id" not in member:
                            continue
                        m_entry = emit_enum_member_entry(
                            folder=folder, rel_path=rel, parent_attr=attr, member=member
                        )
                        if m_entry:
                            entries.append(m_entry)
    return entries


def sort_key(entry: dict) -> tuple:
    return (
        entry.get("folder") or "",
        entry.get("kind") or "",
        entry.get("parent_id") or "",
        entry.get("deprecated_id") or entry.get("deprecated_member_id") or "",
        entry.get("source_file") or "",
        entry.get("source_line") or 0,
    )


def emit_yaml(entries: list[dict], *, tag: str, commit: str) -> str:
    counts: dict[str, Any] = {
        "total_entries": len(entries),
        "by_kind": dict(sorted(Counter(e["kind"] for e in entries).items())),
        "by_folder": dict(sorted(Counter(e["folder"] for e in entries).items())),
    }
    header = {
        "schema_version": 1,
        "dataset": f"semconv-{tag}-programmatic-master",
        "source": {
            "repository": "open-telemetry/semantic-conventions",
            "tag": tag,
            "commit": commit,
        },
        "counts": counts,
    }
    document = {**header, "entries": entries}
    return yaml.safe_dump(
        document, sort_keys=False, default_flow_style=False, allow_unicode=True, width=120
    )


def resolve_commit(submodule: pathlib.Path, override: str | None) -> str:
    if override:
        return override
    try:
        out = subprocess.check_output(
            ["git", "-C", str(submodule), "rev-parse", "HEAD"], stderr=subprocess.DEVNULL
        )
        return out.decode().strip()
    except subprocess.CalledProcessError:
        return ""


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--registry", required=True, help="Path to upstream model/ root")
    parser.add_argument("--out", required=True, help="Path to write master-programmatic.yaml")
    parser.add_argument("--tag", required=True, help="Upstream semconv release tag (e.g. v1.41.0)")
    parser.add_argument("--commit", default=None, help="Override commit SHA (else read from registry submodule)")
    args = parser.parse_args()

    registry = pathlib.Path(args.registry).resolve()
    if not registry.is_dir():
        print(f"[gen-deprecated-lookup] missing registry: {registry}", file=sys.stderr)
        return 1

    submodule_root = registry.parent
    commit = resolve_commit(submodule_root, args.commit)

    entries = walk_registry(registry)
    entries.sort(key=sort_key)
    for entry in entries:
        entry.pop("__line__", None)

    out_path = pathlib.Path(args.out).resolve()
    out_path.parent.mkdir(parents=True, exist_ok=True)
    out_path.write_text(emit_yaml(entries, tag=args.tag, commit=commit), encoding="utf-8")

    print(
        f"[gen-deprecated-lookup] wrote {out_path} "
        f"({len(entries)} entries; tag={args.tag} commit={commit[:12] or '?'})"
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())
