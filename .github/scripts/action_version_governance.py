#!/usr/bin/env python3
# =============================================================================
# Action Version Governance
# -----------------------------------------------------------------------------
# What this file does:
#   - Scans all GitHub Actions workflow YAML files in .github/workflows/.
#   - Extracts `uses:` references for external actions (owner/repo@ref).
#   - Resolves each reference to the latest stable release/tag on GitHub
#     (excluding prerelease/draft/RC tags where possible).
#   - Handles API rate limiting and transient failures with exponential backoff.
#   - Produces a structured JSON report and optional Markdown report.
#   - Optionally rewrites workflow files in-place with --fix.
#   - Respects major version boundaries by default; can override with
#     --allow-major.
#
# Why this exists:
#   - Keeps workflow action versions current and secure.
#   - Prevents silent drift and stale pinned versions.
#   - Enables automated governance in both GitHub.com and GHES.
#
# Compatibility:
#   - Python 3.10+ (available on ubuntu-latest)
#   - No third-party dependencies.
# =============================================================================

from __future__ import annotations

import argparse
import json
import os
import re
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
from dataclasses import dataclass
from pathlib import Path
from typing import Dict, List, Optional, Tuple, Set

USES_PATTERN = re.compile(
    r"""^(\s*uses:\s*)(['"]?)(?P<action>[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+)@(?P<ref>[A-Za-z0-9_.\-/]+)(['"]?)\s*$"""
)
SHA_PATTERN = re.compile(r"^[0-9a-fA-F]{40,64}$")
SEMVER_TAG_PATTERN = re.compile(r"^v?(\d+)\.(\d+)\.(\d+)$")
SEMVER_MAJOR_PATTERN = re.compile(r"^v?(\d+)$")
SEMVER_ANY_PATTERN = re.compile(r"^v?(\d+)(?:\.(\d+))?(?:\.(\d+))?$")
RC_PRERELEASE_HINT = re.compile(
    r"(?:-|\.|_)?(?:rc|alpha|beta|preview|pre|canary|nightly|dev)(?:[-._]?\d*)?$",
    re.IGNORECASE,
)


@dataclass
class ActionRef:
    file: Path
    line_index: int
    line_text: str
    prefix: str
    quote_open: str
    action: str
    current_ref: str
    quote_close: str


@dataclass
class VersionResolution:
    action: str
    current_ref: str
    current_kind: str
    current_semver: Optional[Tuple[int, int, int]]
    latest_stable_ref: Optional[str]
    latest_stable_semver: Optional[Tuple[int, int, int]]
    update_available: bool
    blocked_by_major_boundary: bool
    reason: str
    source: str


def eprint(*args, **kwargs):
    print(*args, file=sys.stderr, **kwargs)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Govern GitHub Action versions in workflow files.")
    parser.add_argument("--repo-root", default=".", help="Repository root path.")
    parser.add_argument("--fix", action="store_true", help="Rewrite workflow files in-place.")
    parser.add_argument("--allow-major", action="store_true", help="Allow major version jumps.")
    parser.add_argument("--report-json", default=".github/action-governance-report.json", help="JSON report output path.")
    parser.add_argument("--report-md", default=".github/action-governance-report.md", help="Markdown report output path.")
    parser.add_argument("--fail-on-drift", action="store_true", help="Exit non-zero if any actionable drift exists.")
    parser.add_argument("--github-api-url", default=os.getenv("GITHUB_API_URL", "").strip(), help="GitHub API base URL override.")
    parser.add_argument("--github-server-url", default=os.getenv("GITHUB_SERVER_URL", "").strip(), help="GitHub server URL override.")
    parser.add_argument("--token", default=os.getenv("GITHUB_TOKEN", "").strip() or os.getenv("GH_TOKEN", "").strip(), help="GitHub token.")
    parser.add_argument("--verbose", action="store_true", help="Verbose logging.")
    return parser.parse_args()


def detect_api_base(github_api_url: str, github_server_url: str) -> str:
    if github_api_url:
        return github_api_url.rstrip("/")
    if github_server_url:
        parsed = urllib.parse.urlparse(github_server_url)
        host = parsed.netloc.lower()
        if host == "github.com":
            return "https://api.github.com"
        return f"{parsed.scheme}://{parsed.netloc}/api/v3"
    return "https://api.github.com"


def list_workflow_files(repo_root: Path) -> List[Path]:
    wf_dir = repo_root / ".github" / "workflows"
    if not wf_dir.exists():
        return []
    files = []
    for p in wf_dir.glob("*.yml"):
        if p.is_file():
            files.append(p)
    for p in wf_dir.glob("*.yaml"):
        if p.is_file():
            files.append(p)
    return sorted(files)


def extract_action_refs_from_file(file_path: Path) -> List[ActionRef]:
    refs: List[ActionRef] = []
    lines = file_path.read_text(encoding="utf-8").splitlines()
    for idx, line in enumerate(lines):
        m = USES_PATTERN.match(line)
        if not m:
            continue
        action = m.group("action")
        ref = m.group("ref")
        if action.startswith("./") or action.startswith("../"):
            continue
        refs.append(
            ActionRef(
                file=file_path,
                line_index=idx,
                line_text=line,
                prefix=m.group(1),
                quote_open=m.group(2) or "",
                action=action,
                current_ref=ref,
                quote_close=m.group(5) or "",
            )
        )
    return refs


def kind_of_ref(ref: str) -> str:
    if SHA_PATTERN.match(ref):
        return "sha"
    if SEMVER_ANY_PATTERN.match(ref):
        return "semver"
    return "other"


def parse_semver(ref: str) -> Optional[Tuple[int, int, int]]:
    m = SEMVER_ANY_PATTERN.match(ref)
    if not m:
        return None
    major = int(m.group(1))
    minor = int(m.group(2) or 0)
    patch = int(m.group(3) or 0)
    return (major, minor, patch)


class GitHubClient:
    def __init__(self, api_base: str, token: str, verbose: bool = False):
        self.api_base = api_base.rstrip("/")
        self.token = token.strip()
        self.verbose = verbose

    def _request_json(self, url: str, max_attempts: int = 6):
        backoff = 1.0
        for attempt in range(1, max_attempts + 1):
            req = urllib.request.Request(url, method="GET")
            req.add_header("Accept", "application/vnd.github+json")
            req.add_header("User-Agent", "action-version-governance/1.0")
            if self.token:
                req.add_header("Authorization", f"Bearer {self.token}")

            try:
                with urllib.request.urlopen(req, timeout=30) as resp:
                    data = resp.read().decode("utf-8")
                    return json.loads(data), resp.headers, resp.status
            except urllib.error.HTTPError as ex:
                status = ex.code
                body = ex.read().decode("utf-8", errors="replace") if ex.fp else ""
                headers = ex.headers or {}

                if status == 404:
                    return None, headers, 404

                # Rate limit handling
                if status in (403, 429):
                    reset = headers.get("X-RateLimit-Reset")
                    retry_after = headers.get("Retry-After")
                    sleep_for = backoff
                    if retry_after:
                        try:
                            sleep_for = max(float(retry_after), sleep_for)
                        except ValueError:
                            pass
                    elif reset:
                        try:
                            reset_epoch = int(reset)
                            now = int(time.time())
                            sleep_for = max(reset_epoch - now + 1, sleep_for)
                        except ValueError:
                            pass

                    if attempt < max_attempts:
                        if self.verbose:
                            eprint(f"[governance] rate-limited ({status}); sleeping {sleep_for:.1f}s then retry")
                        time.sleep(sleep_for)
                        backoff = min(backoff * 2, 60.0)
                        continue

                # Retry transient errors
                if status >= 500 and attempt < max_attempts:
                    if self.verbose:
                        eprint(f"[governance] server error {status}; retry in {backoff:.1f}s")
                    time.sleep(backoff)
                    backoff = min(backoff * 2, 60.0)
                    continue

                raise RuntimeError(f"GitHub API request failed: {url} status={status} body={body}")
            except urllib.error.URLError as ex:
                if attempt < max_attempts:
                    if self.verbose:
                        eprint(f"[governance] network error {ex}; retry in {backoff:.1f}s")
                    time.sleep(backoff)
                    backoff = min(backoff * 2, 60.0)
                    continue
                raise RuntimeError(f"Network error calling GitHub API {url}: {ex}")

        raise RuntimeError(f"Exceeded retry attempts for GitHub API: {url}")

    def get_latest_release_tag(self, action: str) -> Tuple[Optional[str], str]:
        url = f"{self.api_base}/repos/{action}/releases/latest"
        data, _, status = self._request_json(url)
        if status == 404 or data is None:
            return None, "releases_latest_404"
        tag = (data.get("tag_name") or "").strip()
        if not tag:
            return None, "releases_latest_empty"
        return tag, "releases_latest"

    def get_tags(self, action: str, per_page: int = 100, max_pages: int = 3) -> List[str]:
        tags: List[str] = []
        for page in range(1, max_pages + 1):
            url = f"{self.api_base}/repos/{action}/tags?per_page={per_page}&page={page}"
            data, _, status = self._request_json(url)
            if status == 404 or data is None:
                break
            if not isinstance(data, list) or not data:
                break
            for t in data:
                name = (t.get("name") or "").strip()
                if name:
                    tags.append(name)
            if len(data) < per_page:
                break
        return tags


def stable_tag_score(tag: str) -> Optional[Tuple[int, int, int]]:
    if RC_PRERELEASE_HINT.search(tag):
        return None
    m = SEMVER_TAG_PATTERN.match(tag)
    if not m:
        return None
    return int(m.group(1)), int(m.group(2)), int(m.group(3))


def resolve_latest_stable(client: GitHubClient, action: str) -> Tuple[Optional[str], Optional[Tuple[int, int, int]], str]:
    latest_tag, source = client.get_latest_release_tag(action)
    if latest_tag:
        score = stable_tag_score(latest_tag)
        if score:
            return latest_tag, score, source
    # Fallback to tags endpoint
    tags = client.get_tags(action)
    best_tag = None
    best_score = None
    for t in tags:
        s = stable_tag_score(t)
        if not s:
            continue
        if best_score is None or s > best_score:
            best_score = s
            best_tag = t
    if best_tag:
        return best_tag, best_score, f"{source}+tags_fallback"
    return None, None, f"{source}+tags_no_stable"


def should_update(current_ref: str, current_semver: Optional[Tuple[int, int, int]], latest_ref: Optional[str], latest_semver: Optional[Tuple[int, int, int]], allow_major: bool) -> Tuple[bool, bool, str]:
    if not latest_ref or not latest_semver:
        return False, False, "no_stable_target"

    if current_semver is None:
        return True, False, "non_semver_current_ref"

    if latest_semver <= current_semver:
        return False, False, "up_to_date_or_newer"

    if not allow_major and latest_semver[0] != current_semver[0]:
        return False, True, "major_update_blocked"

    return True, False, "update_available"


def apply_fixes(refs: List[ActionRef], resolutions: Dict[Tuple[str, str], VersionResolution], allow_major: bool) -> int:
    per_file: Dict[Path, List[ActionRef]] = {}
    for r in refs:
        per_file.setdefault(r.file, []).append(r)

    changed_files = 0
    for file_path, file_refs in per_file.items():
        lines = file_path.read_text(encoding="utf-8").splitlines()
        changed = False
        for ref in file_refs:
            key = (ref.action, ref.current_ref)
            res = resolutions.get(key)
            if not res or not res.update_available or not res.latest_stable_ref:
                continue
            if res.blocked_by_major_boundary and not allow_major:
                continue
            new_line = f"{ref.prefix}{ref.quote_open}{ref.action}@{res.latest_stable_ref}{ref.quote_close}"
            if lines[ref.line_index] != new_line:
                lines[ref.line_index] = new_line
                changed = True

        if changed:
            file_path.write_text("\n".join(lines) + "\n", encoding="utf-8")
            changed_files += 1

    return changed_files


def markdown_report(report: dict) -> str:
    lines = []
    lines.append("# GitHub Action Version Governance Report")
    lines.append("")
    lines.append(f"- Generated at: `{report['generated_at']}`")
    lines.append(f"- API base: `{report['api_base']}`")
    lines.append(f"- Workflows scanned: `{report['workflows_scanned']}`")
    lines.append(f"- Action references scanned: `{report['action_references_scanned']}`")
    lines.append(f"- Drift detected: `{report['drift_detected']}`")
    lines.append("")
    lines.append("## Findings")
    lines.append("")
    lines.append("| Action | Current | Latest Stable | Update | Blocked Major | Source | Reason |")
    lines.append("|---|---:|---:|---:|---:|---|---|")
    for item in report["findings"]:
        lines.append(
            f"| `{item['action']}` | `{item['current_ref']}` | `{item.get('latest_stable_ref') or '-'}` | "
            f"`{'yes' if item['update_available'] else 'no'}` | "
            f"`{'yes' if item['blocked_by_major_boundary'] else 'no'}` | "
            f"`{item['source']}` | `{item['reason']}` |"
        )
    lines.append("")
    lines.append("## File-level changes")
    lines.append("")
    for file_item in report["files"]:
        lines.append(f"### `{file_item['file']}`")
        if not file_item["entries"]:
            lines.append("- No external actions detected.")
            lines.append("")
            continue
        for e in file_item["entries"]:
            lines.append(
                f"- `{e['action']}`: `{e['current_ref']}` -> `{e.get('latest_stable_ref') or '-'}` "
                f"(update: {'yes' if e['update_available'] else 'no'}, blocked-major: {'yes' if e['blocked_by_major_boundary'] else 'no'})"
            )
        lines.append("")
    return "\n".join(lines).rstrip() + "\n"


def main():
    args = parse_args()
    repo_root = Path(args.repo_root).resolve()
    api_base = detect_api_base(args.github_api_url, args.github_server_url)
    client = GitHubClient(api_base=api_base, token=args.token or "", verbose=args.verbose)

    workflow_files = list_workflow_files(repo_root)
    all_refs: List[ActionRef] = []
    for wf in workflow_files:
        all_refs.extend(extract_action_refs_from_file(wf))

    unique_pairs: Set[Tuple[str, str]] = {(r.action, r.current_ref) for r in all_refs}
    resolutions: Dict[Tuple[str, str], VersionResolution] = {}

    action_cache: Dict[str, Tuple[Optional[str], Optional[Tuple[int, int, int]], str]] = {}
    for action, current_ref in sorted(unique_pairs):
        if action not in action_cache:
            action_cache[action] = resolve_latest_stable(client, action)
        latest_ref, latest_semver, source = action_cache[action]

        current_kind = kind_of_ref(current_ref)
        current_semver = parse_semver(current_ref)
        update_available, blocked_major, reason = should_update(
            current_ref=current_ref,
            current_semver=current_semver,
            latest_ref=latest_ref,
            latest_semver=latest_semver,
            allow_major=args.allow_major,
        )
        resolutions[(action, current_ref)] = VersionResolution(
            action=action,
            current_ref=current_ref,
            current_kind=current_kind,
            current_semver=current_semver,
            latest_stable_ref=latest_ref,
            latest_stable_semver=latest_semver,
            update_available=update_available,
            blocked_by_major_boundary=blocked_major,
            reason=reason,
            source=source,
        )

    changed_files = 0
    if args.fix:
        changed_files = apply_fixes(all_refs, resolutions, args.allow_major)

    files_section = []
    for wf in workflow_files:
        entries = []
        wf_refs = [r for r in all_refs if r.file == wf]
        for r in wf_refs:
            res = resolutions[(r.action, r.current_ref)]
            entries.append(
                {
                    "line": r.line_index + 1,
                    "action": r.action,
                    "current_ref": r.current_ref,
                    "latest_stable_ref": res.latest_stable_ref,
                    "update_available": res.update_available,
                    "blocked_by_major_boundary": res.blocked_by_major_boundary,
                    "reason": res.reason,
                    "source": res.source,
                }
            )
        files_section.append(
            {
                "file": str(wf.relative_to(repo_root)),
                "entries": entries,
            }
        )

    findings = []
    drift = False
    for key in sorted(resolutions.keys()):
        res = resolutions[key]
        findings.append(
            {
                "action": res.action,
                "current_ref": res.current_ref,
                "current_kind": res.current_kind,
                "latest_stable_ref": res.latest_stable_ref,
                "update_available": res.update_available,
                "blocked_by_major_boundary": res.blocked_by_major_boundary,
                "reason": res.reason,
                "source": res.source,
            }
        )
        if res.update_available:
            drift = True

    report = {
        "generated_at": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
        "api_base": api_base,
        "workflows_scanned": len(workflow_files),
        "action_references_scanned": len(all_refs),
        "unique_action_refs": len(unique_pairs),
        "allow_major": args.allow_major,
        "fix_applied": args.fix,
        "changed_files": changed_files,
        "drift_detected": drift,
        "findings": findings,
        "files": files_section,
    }

    report_json_path = repo_root / args.report_json
    report_json_path.parent.mkdir(parents=True, exist_ok=True)
    report_json_path.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")

    report_md_path = repo_root / args.report_md
    report_md_path.parent.mkdir(parents=True, exist_ok=True)
    report_md_path.write_text(markdown_report(report), encoding="utf-8")

    print(f"report_json={report_json_path}")
    print(f"report_md={report_md_path}")
    print(f"drift_detected={'true' if drift else 'false'}")
    print(f"changed_files={changed_files}")

    if args.fail_on_drift and drift:
        sys.exit(2)


if __name__ == "__main__":
    main()