"""Sentry perf JSON -> qyl feature gap analysis.

Strips noise (pendo, amplitude, stripe, statuspage, ingest, avatars, zendesk).
Extracts Sentry API paths, normalizes org slugs, and cross-references against
the seer-knowledge-base.md to find undocumented features.
"""

import json
import re
from pathlib import Path
import pandas as pd

FOLDERS = [
    "/Users/ancplua/Downloads/seer/1",
    "/Users/ancplua/Downloads/seer/2",
    "/Users/ancplua/Downloads/seer/3",
]

# Noise domains — not Sentry API surface
NOISE_DOMAINS = {
    "data.pendo.io",
    "pendo-static-5634074999128064.storage.googleapis.com",
    "api2.amplitude.com",
    "js.stripe.com",
    "t687h3m0nh65.statuspage.io",
    "ekr.zdassets.com",
    "avatars.githubusercontent.com",
    "o1.ingest.sentry.io",
    "reload.getsentry.net",
}

# Known endpoints from seer-knowledge-base.md (normalized path patterns)
KNOWN_ENDPOINTS = {
    # MCP Tool Catalog - inspect skill
    "/api/0/organizations/{org}/",
    "/api/0/organizations/",
    "/api/0/organizations/{org}/projects/",
    "/api/0/organizations/{org}/teams/",
    # MCP Tool Catalog - triage skill
    # (update_issue is write-only, no GET equivalent in waterfall)
    # Seer API Surface
    "/api/0/organizations/{org}/seer/onboarding-check/",
    "/api/0/organizations/{org}/seer/setup-check/",
    # Autofix Settings API
    "/api/0/organizations/{org}/autofix/automation-settings/",
    # Integrations (known from knowledge base)
    "/api/0/organizations/{org}/integrations/",
    "/api/0/organizations/{org}/integrations/{id}/repos/",
    # Customers (billing)
    "/api/0/customers/{org}/",
    "/api/0/customers/{org}/billing-config/",
}


def normalize_path(path: str) -> str:
    """Replace org slugs and numeric IDs with placeholders."""
    # Replace known org slug
    path = re.sub(r"/technikum-wien-gmbh/", "/{org}/", path)
    # Replace numeric IDs (e.g., /integrations/379517/)
    path = re.sub(r"/\d{4,}/", "/{id}/", path)
    return path


# --- Load and deduplicate ---
all_requests: list[dict] = []
seen: set[tuple] = set()

for folder in FOLDERS:
    for file in Path(folder).rglob("*.json"):
        with open(file, encoding="utf-8") as f:
            data = json.load(f)

        for domain, req_list in data.items():
            if domain in NOISE_DOMAINS:
                continue
            for req in req_list:
                key = (domain, req.get("path"), req.get("method"))
                if key not in seen:
                    seen.add(key)
                    row = req.copy()
                    row["domain"] = domain
                    row["normalized_path"] = normalize_path(req.get("path", ""))
                    row["source_file"] = file.name
                    all_requests.append(row)

df = pd.DataFrame(all_requests)
df = df[["domain", "ms", "path", "normalized_path", "url", "method", "kb", "source_file"]]

# --- Feature gap analysis ---
unique_paths = set(df["normalized_path"].unique())
known = unique_paths & KNOWN_ENDPOINTS
unknown = unique_paths - KNOWN_ENDPOINTS

print(f"Sentry API endpoints found:  {len(unique_paths)}")
print(f"Already documented:          {len(known)}")
print(f"NOT in knowledge base:       {len(unknown)}")

print("\n" + "=" * 70)
print("DOCUMENTED (already in seer-knowledge-base.md)")
print("=" * 70)
for p in sorted(known):
    print(f"  [ok] {p}")

print("\n" + "=" * 70)
print("UNDOCUMENTED — FEATURE GAP (add to knowledge base)")
print("=" * 70)
for p in sorted(unknown):
    # Find example row for context
    example = df[df["normalized_path"] == p].iloc[0]
    print(f"  [??] {p}")
    print(f"       ms={example['ms']}, method={example['method']}, domain={example['domain']}")

# --- Slowest undocumented endpoints ---
undoc_df = df[df["normalized_path"].isin(unknown)].sort_values("ms", ascending=False)
print("\n" + "=" * 70)
print("UNDOCUMENTED ENDPOINTS BY LATENCY")
print("=" * 70)
print(undoc_df[["normalized_path", "method", "ms", "domain"]].to_string(index=False))

# --- Feature categories ---
print("\n" + "=" * 70)
print("FEATURE CATEGORIES (undocumented)")
print("=" * 70)

categories: dict[str, list[str]] = {}
for p in sorted(unknown):
    parts = p.strip("/").split("/")
    # Try to extract the feature name from the path
    # /api/0/organizations/{org}/FEATURE/ or /api/0/assistant/
    if "{org}" in parts:
        idx = parts.index("{org}")
        if idx + 1 < len(parts):
            cat = parts[idx + 1]
        else:
            cat = "org-root"
    elif len(parts) >= 3:
        cat = parts[2] if parts[2] != "{org}" else parts[3] if len(parts) > 3 else "unknown"
    else:
        cat = "other"
    categories.setdefault(cat, []).append(p)

for cat, paths in sorted(categories.items()):
    print(f"\n  {cat}:")
    for p in paths:
        print(f"    - {p}")

# --- Export ---
out = Path("/Users/ancplua/Downloads/seer")
df.to_csv(out / "sentry_api_surface.csv", index=False)

gap_df = pd.DataFrame([{"path": p} for p in sorted(unknown)])
gap_df.to_csv(out / "sentry_feature_gaps.csv", index=False)

print("\n" + "=" * 70)
print("EXPORT")
print("=" * 70)
print(f"  -> {out / 'sentry_api_surface.csv'}  (all Sentry API requests)")
print(f"  -> {out / 'sentry_feature_gaps.csv'}  (undocumented endpoints only)")
