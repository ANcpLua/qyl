# OTel Semantic Conventions Weaver Pipeline — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a donation-ready, Weaver-driven OTel Semantic Conventions generation pipeline that produces three NuGet packages: stable OTel attrs, incubating OTel attrs, and qyl custom attrs — all generated at build time from upstream YAML, with zero hand-maintained constants.

**Architecture:** Two Weaver registry runs (upstream OTel model → two NuGet packages, qyl model → one NuGet package) using `application_mode: each` (one C# file per root namespace, confirmed working). `attr.deprecated` is natively structured by Weaver (`reason`/`renamed_to`/`note`) — no external YAML lookup needed in templates. The existing `qyl` template set (TS, SQL, JSON) is untouched throughout.

**Tech Stack:** Weaver v0.22.1, Jinja2, C# 14, .NET 10, ANcpLua.NET.Sdk, NUKE 10.1.0, open-telemetry/semantic-conventions v1.40.0 submodule at `.tools/semconv-upstream`

---

## File Map

### Create
- `packages/Qyl.OpenTelemetry.SemanticConventions/Qyl.OpenTelemetry.SemanticConventions.csproj`
- `packages/Qyl.OpenTelemetry.SemanticConventions.Incubating/Qyl.OpenTelemetry.SemanticConventions.Incubating.csproj`
- `packages/Qyl.SemanticConventions/Qyl.SemanticConventions.csproj`
- `eng/semconv/templates/registry/csharp_stable/weaver.yaml`
- `eng/semconv/templates/registry/csharp_stable/attributes.cs.j2`
- `eng/semconv/templates/registry/csharp_stable/schema_url.cs.j2`
- `eng/semconv/templates/registry/csharp_incubating/weaver.yaml`
- `eng/semconv/templates/registry/csharp_incubating/attributes.cs.j2`
- `eng/semconv/templates/registry/csharp_incubating/schema_url.cs.j2`
- `eng/semconv/templates/registry/csharp_qyl/weaver.yaml`
- `eng/semconv/templates/registry/csharp_qyl/attributes.cs.j2`
- `eng/semconv/templates/registry/csharp_qyl/schema_url.cs.j2`
- `eng/semconv/qyl/model/capability.yaml`
- `eng/semconv/qyl/model/run.yaml`
- `eng/semconv/qyl/model/project.yaml`
- `eng/semconv/qyl/model/team.yaml`
- `eng/semconv/qyl/model/issue.yaml`
- `eng/semconv/qyl/model/triage.yaml`
- `eng/semconv/qyl/model/fix_run.yaml`
- `eng/semconv/qyl/model/api_key.yaml`
- `eng/semconv/qyl/schemas/1.0.0.yaml`
- `eng/semconv/deprecated-lookup/semconv-v1.40.0-attributes.yaml`
- `eng/semconv/deprecated-lookup/semconv-v1.40.0-metrics.yaml`
- `eng/semconv/deprecated-lookup/semconv-v1.40.0-events.yaml`
- `eng/semconv/deprecated-lookup/semconv-v1.40.0-enum-members.yaml`
- `eng/semconv/deprecated-lookup/semconv-v1.40.0-entities.yaml`
- `Makefile` (new or add target to existing)

### Modify
- `qyl.slnx` — add three new package `<Project>` entries
- `eng/build/BuildPipeline.cs` — add `GenerateSemconvCsharp` target, update `Generate`
- `eng/semconv/run-weaver.sh` — add three new Weaver runs (or delegate to new script)

### Generated (never hand-edit)
- `packages/Qyl.OpenTelemetry.SemanticConventions/Attributes/**/*.g.cs`
- `packages/Qyl.OpenTelemetry.SemanticConventions/SchemaUrl.g.cs`
- `packages/Qyl.OpenTelemetry.SemanticConventions/SchemaVersion.g.cs`
- `packages/Qyl.OpenTelemetry.SemanticConventions.Incubating/Attributes/**/*.g.cs`
- `packages/Qyl.OpenTelemetry.SemanticConventions.Incubating/SchemaUrl.g.cs`
- `packages/Qyl.OpenTelemetry.SemanticConventions.Incubating/SchemaVersion.g.cs`
- `packages/Qyl.SemanticConventions/Attributes/**/*.g.cs`
- `packages/Qyl.SemanticConventions/SchemaUrl.g.cs`

### Runtime cutover (later phases)
- `packages/Qyl.Contracts/Attributes/GenAiAttributes.cs` — deleted, callers updated
- `packages/Qyl.Contracts/Attributes/DbAttributes.cs` — deleted, callers updated
- `packages/Qyl.Contracts/Attributes/McpAttributes.cs` — deleted, callers updated
- All `services/**/*.cs` files with `"qyl.*"` strings → replaced with typed constants

---

## Phase 1 — Package Scaffold + One Namespace End-to-End

### Task 1: Init semconv-upstream submodule in worktree

**Files:** None (git operation)

- [ ] **Step 1: Init the submodule**

```bash
cd /path/to/worktree
git submodule update --init .tools/semconv-upstream
```

Expected: `.tools/semconv-upstream/model/` directory exists with OTel model YAML files.

- [ ] **Step 2: Verify submodule commit**

```bash
git -C .tools/semconv-upstream rev-parse HEAD
```

Expected output: `7fe537301d17919af7d7eb65b32e9be35da2c497`

---

### Task 2: Scaffold three package directories and csproj files

**Files:**
- Create: `packages/Qyl.OpenTelemetry.SemanticConventions/Qyl.OpenTelemetry.SemanticConventions.csproj`
- Create: `packages/Qyl.OpenTelemetry.SemanticConventions.Incubating/Qyl.OpenTelemetry.SemanticConventions.Incubating.csproj`
- Create: `packages/Qyl.SemanticConventions/Qyl.SemanticConventions.csproj`

- [ ] **Step 1: Create package directory for stable**

```bash
mkdir -p packages/Qyl.OpenTelemetry.SemanticConventions/Attributes \
         packages/Qyl.OpenTelemetry.SemanticConventions/schemas
```

- [ ] **Step 2: Write stable csproj**

Create `packages/Qyl.OpenTelemetry.SemanticConventions/Qyl.OpenTelemetry.SemanticConventions.csproj`:

```xml
<Project Sdk="ANcpLua.NET.Sdk">

  <PropertyGroup>
    <TargetFrameworks>net10.0;netstandard2.0</TargetFrameworks>
    <TargetFramework></TargetFramework>
    <RootNamespace>Qyl.OpenTelemetry.SemanticConventions</RootNamespace>
    <PackageId>Qyl.OpenTelemetry.SemanticConventions</PackageId>
    <Description>OpenTelemetry Semantic Conventions stable attribute keys (v1.40.0). Generated by Weaver from open-telemetry/semantic-conventions.</Description>
    <Authors>ancplua</Authors>
    <IsPackable>true</IsPackable>
    <GeneratePackageOnBuild>false</GeneratePackageOnBuild>
  </PropertyGroup>

  <ItemGroup>
    <EmbeddedResource Include="schemas/1.40.0.yaml" LogicalName="otel-semconv-1.40.0.yaml"/>
  </ItemGroup>

</Project>
```

- [ ] **Step 3: Create incubating package directory + csproj**

```bash
mkdir -p packages/Qyl.OpenTelemetry.SemanticConventions.Incubating/Attributes \
         packages/Qyl.OpenTelemetry.SemanticConventions.Incubating/schemas
```

Create `packages/Qyl.OpenTelemetry.SemanticConventions.Incubating/Qyl.OpenTelemetry.SemanticConventions.Incubating.csproj`:

```xml
<Project Sdk="ANcpLua.NET.Sdk">

  <PropertyGroup>
    <TargetFrameworks>net10.0;netstandard2.0</TargetFrameworks>
    <TargetFramework></TargetFramework>
    <RootNamespace>Qyl.OpenTelemetry.SemanticConventions.Incubating</RootNamespace>
    <PackageId>Qyl.OpenTelemetry.SemanticConventions.Incubating</PackageId>
    <Description>OpenTelemetry Semantic Conventions experimental/development attribute keys (v1.40.0). Generated by Weaver. These attributes are not yet stable and may change.</Description>
    <Authors>ancplua</Authors>
    <IsPackable>true</IsPackable>
    <GeneratePackageOnBuild>false</GeneratePackageOnBuild>
  </PropertyGroup>

  <ItemGroup>
    <EmbeddedResource Include="schemas/1.40.0.yaml" LogicalName="otel-semconv-1.40.0.yaml"/>
  </ItemGroup>

</Project>
```

- [ ] **Step 4: Create qyl package directory + csproj**

```bash
mkdir -p packages/Qyl.SemanticConventions/Attributes \
         packages/Qyl.SemanticConventions/schemas
```

Create `packages/Qyl.SemanticConventions/Qyl.SemanticConventions.csproj`:

```xml
<Project Sdk="ANcpLua.NET.Sdk">

  <PropertyGroup>
    <TargetFrameworks>net10.0;netstandard2.0</TargetFrameworks>
    <TargetFramework></TargetFramework>
    <RootNamespace>Qyl.SemanticConventions</RootNamespace>
    <PackageId>Qyl.SemanticConventions</PackageId>
    <Description>qyl custom semantic convention attribute keys. Generated by Weaver from qyl's own attribute registry.</Description>
    <Authors>ancplua</Authors>
    <IsPackable>true</IsPackable>
    <GeneratePackageOnBuild>false</GeneratePackageOnBuild>
  </PropertyGroup>

  <ItemGroup>
    <EmbeddedResource Include="schemas/1.0.0.yaml" LogicalName="qyl-semconv-1.0.0.yaml"/>
  </ItemGroup>

</Project>
```

---

### Task 3: Add all three packages to qyl.slnx

**Files:**
- Modify: `qyl.slnx`

- [ ] **Step 1: Open qyl.slnx and add the three package entries**

In `qyl.slnx`, find the block containing the existing package entries (currently lines 6-8 list `Qyl.Contracts`, `Qyl.Client`, `Qyl.OpenTelemetry.Extensions`). Add the three new packages immediately after:

```xml
        <Project Path="packages/Qyl.OpenTelemetry.SemanticConventions/Qyl.OpenTelemetry.SemanticConventions.csproj" />
        <Project Path="packages/Qyl.OpenTelemetry.SemanticConventions.Incubating/Qyl.OpenTelemetry.SemanticConventions.Incubating.csproj" />
        <Project Path="packages/Qyl.SemanticConventions/Qyl.SemanticConventions.csproj" />
```

- [ ] **Step 2: Verify solution file parses**

```bash
dotnet sln qyl.slnx list
```

Expected: the three new packages appear in the list.

---

### Task 4: Create csharp_stable Weaver template set

**Files:**
- Create: `eng/semconv/templates/registry/csharp_stable/weaver.yaml`
- Create: `eng/semconv/templates/registry/csharp_stable/schema_url.cs.j2`
- Create: `eng/semconv/templates/registry/csharp_stable/schema_version.cs.j2`

- [ ] **Step 1: Write csharp_stable/weaver.yaml**

Create `eng/semconv/templates/registry/csharp_stable/weaver.yaml`:

```yaml
# Weaver template config for Qyl.OpenTelemetry.SemanticConventions (stable attrs only).
# Outputs one .g.cs per root_namespace, plus SchemaUrl.g.cs and SchemaVersion.g.cs.
# Stability filter: stability == "stable" only (development + experimental → Incubating package).

whitespace_control:
  trim_blocks: true
  lstrip_blocks: true

params:
  schema_url: "https://opentelemetry.io/schemas/1.40.0"
  schema_version: "1.40.0"
  semconv_commit: "7fe537301d17919af7d7eb65b32e9be35da2c497"
  package_namespace: "Qyl.OpenTelemetry.SemanticConventions"

  # Reserved attrs: build will fail if any are absent from generated output.
  # Checked by the NUKE verify step, not in the template itself.
  reserved_attrs:
    - "error.type"
    - "exception.message"
    - "exception.stacktrace"
    - "exception.type"
    - "server.address"
    - "server.port"
    - "service.name"
    - "service.instance.id"
    - "telemetry.sdk.language"
    - "telemetry.sdk.name"
    - "telemetry.sdk.version"
    - "url.scheme"

templates:
  - template: attributes.cs.j2
    filter: semconv_grouped_attributes
    application_mode: each
    file_name: "Attributes/{{ ctx.root_namespace | pascal_case }}/{{ ctx.root_namespace | pascal_case }}Attributes.g.cs"

  - template: schema_url.cs.j2
    filter: semconv_grouped_attributes
    application_mode: single
    file_name: "SchemaUrl.g.cs"

  - template: schema_version.cs.j2
    filter: semconv_grouped_attributes
    application_mode: single
    file_name: "SchemaVersion.g.cs"
```

- [ ] **Step 2: Write schema_url.cs.j2**

Create `eng/semconv/templates/registry/csharp_stable/schema_url.cs.j2`:

```jinja
// <auto-generated/>
// Generated by qyl's Weaver pipeline from open-telemetry/semantic-conventions@{{ params.semconv_commit }}
// Schema: {{ params.schema_url }}
// Licensed under Apache-2.0 (inherited from OpenTelemetry upstream)
// </auto-generated>

// Copyright (c) 2025-2026 ancplua

namespace {{ params.package_namespace }};

/// <summary>Schema URL for OpenTelemetry Semantic Conventions {{ params.schema_version }}.</summary>
public static partial class SchemaUrl
{
    /// <summary>The schema URL for OTel semconv {{ params.schema_version }}.</summary>
    public const string Current = "{{ params.schema_url }}";
}
```

- [ ] **Step 3: Write schema_version.cs.j2**

Create `eng/semconv/templates/registry/csharp_stable/schema_version.cs.j2`:

```jinja
// <auto-generated/>
// Generated by qyl's Weaver pipeline from open-telemetry/semantic-conventions@{{ params.semconv_commit }}
// Schema: {{ params.schema_url }}
// Licensed under Apache-2.0 (inherited from OpenTelemetry upstream)
// </auto-generated>

// Copyright (c) 2025-2026 ancplua

namespace {{ params.package_namespace }};

/// <summary>Schema version for OpenTelemetry Semantic Conventions.</summary>
public static partial class SchemaVersion
{
    /// <summary>The semconv version string.</summary>
    public const string Current = "{{ params.schema_version }}";
}
```

---

### Task 5: Write csharp_stable attributes.cs.j2 template

**Files:**
- Create: `eng/semconv/templates/registry/csharp_stable/attributes.cs.j2`

The template produces one file per root_namespace containing ONLY `stability: stable` attributes. Empty namespaces (no stable attrs) produce an empty file but still compile — they're skipped by a guard at the top.

- [ ] **Step 1: Write attributes.cs.j2**

Create `eng/semconv/templates/registry/csharp_stable/attributes.cs.j2`:

```jinja
{%- set stable_attrs = ctx.attributes | selectattr("stability", "equalto", "stable") | list -%}
{%- if stable_attrs | length > 0 -%}
// <auto-generated/>
// Generated by qyl's Weaver pipeline from open-telemetry/semantic-conventions@{{ params.semconv_commit }}
// Schema: {{ params.schema_url }}
// Licensed under Apache-2.0 (inherited from OpenTelemetry upstream)
// </auto-generated>

// Copyright (c) 2025-2026 ancplua

namespace {{ params.package_namespace }}.Attributes.{{ ctx.root_namespace | pascal_case }};

/// <summary>{{ ctx.display_name | default(ctx.root_namespace | pascal_case + " Attributes") }}.</summary>
public static class {{ ctx.root_namespace | pascal_case }}Attributes
{
{% for attr in stable_attrs | sort(attribute="name") %}
    /// <summary>{{ attr.brief | replace('\n', ' ') | replace('"', "'") | trim }}.</summary>
{% if attr.note is defined and attr.note %}
    /// <remarks>{{ attr.note | replace('\n', ' ') | replace('"', "'") | truncate(500, True, '...') | trim }}</remarks>
{% endif %}
{% if attr.deprecated %}
{% if attr.deprecated.reason == "renamed" %}
    [System.Obsolete("Replaced by {{ attr.deprecated.renamed_to }}.", false)]
{% elif attr.deprecated.reason == "obsoleted" %}
    [System.Obsolete("Removed, no replacement.", false)]
{% else %}
    [System.Obsolete({{ attr.deprecated.note | replace('\n', ' ') | trim | tojson }}, false)]
{% endif %}
{% endif %}
    public const string {{ attr.name[(ctx.root_namespace | length + 1):] | replace(".", "_") | pascal_case }} = "{{ attr.name }}";
{% if attr.type is mapping and attr.type.members is defined %}

    /// <summary>Values for <see cref="{{ attr.name[(ctx.root_namespace | length + 1):] | replace(".", "_") | pascal_case }}"/>.</summary>
    public static class {{ attr.name[(ctx.root_namespace | length + 1):] | replace(".", "_") | pascal_case }}Values
    {
{% for member in attr.type.members | sort(attribute="value") %}
        /// <summary>{{ member.brief | replace('\n', ' ') | replace('"', "'") | trim }}.</summary>
{% if member.deprecated %}
        [System.Obsolete({{ member.deprecated | string | tojson }}, false)]
{% endif %}
        public const string {{ member.id | pascal_case }} = {{ member.value | tojson }};
{% endfor %}
    }
{% endif %}

{% endfor %}
}
{%- endif -%}
```

---

### Task 6: Run Weaver for the stable template and verify http namespace

**Files:** None (verify step)

- [ ] **Step 1: Bootstrap Weaver if not already done**

```bash
bash eng/semconv/bootstrap-weaver.sh
```

Expected: Weaver binary present at `.tools/weaver/weaver-aarch64-apple-darwin/weaver` (or platform equivalent).

- [ ] **Step 2: Run Weaver manually for stable template**

```bash
REPO_ROOT="$(pwd)"
WEAVER_BIN=".tools/weaver/weaver-$(uname -s | tr A-Z a-z | sed 's/darwin/apple-darwin/')-$(uname -m)/weaver"
# On arm64 mac:
WEAVER_BIN=".tools/weaver/weaver-aarch64-apple-darwin/weaver"

"$WEAVER_BIN" registry generate \
  --registry .tools/semconv-upstream/model \
  --templates eng/semconv/templates/registry \
  csharp_stable \
  packages/Qyl.OpenTelemetry.SemanticConventions
```

Expected: dozens of `✔ Generated file "...Attributes.g.cs"` lines + `SchemaUrl.g.cs` + `SchemaVersion.g.cs`.

- [ ] **Step 3: Verify http namespace output**

```bash
cat packages/Qyl.OpenTelemetry.SemanticConventions/Attributes/Http/HttpAttributes.g.cs
```

Expected output contains:
- Auto-generated header with schema URL
- `namespace Qyl.OpenTelemetry.SemanticConventions.Attributes.Http;`
- `public static class HttpAttributes`
- `public const string RequestMethod = "http.request.method";`
- `public const string ResponseStatusCode = "http.response.status_code";`
- `public static class RequestMethodValues` with `Get`, `Post`, `Put`, `Delete`, `Head`, `Options`, `Patch` etc.

- [ ] **Step 4: Verify SchemaUrl.g.cs**

```bash
cat packages/Qyl.OpenTelemetry.SemanticConventions/SchemaUrl.g.cs
```

Expected: contains `public const string Current = "https://opentelemetry.io/schemas/1.40.0";`

- [ ] **Step 5: Build the stable package**

```bash
dotnet build packages/Qyl.OpenTelemetry.SemanticConventions/Qyl.OpenTelemetry.SemanticConventions.csproj --tl:off 2>&1 | tee /tmp/semconv-stable-build.log
grep -E "error|warning" /tmp/semconv-stable-build.log | grep -v "warning CS1591" | head -20
```

Expected: Build succeeds. CS1591 (missing XML doc) warnings are acceptable at this stage; all others must be 0.

---

### Task 7: Commit Phase 1

- [ ] **Step 1: Stage Phase 1 artifacts**

```bash
git add packages/Qyl.OpenTelemetry.SemanticConventions/ \
        packages/Qyl.OpenTelemetry.SemanticConventions.Incubating/ \
        packages/Qyl.SemanticConventions/ \
        eng/semconv/templates/registry/csharp_stable/ \
        qyl.slnx
git status
```

- [ ] **Step 2: Commit Phase 1**

```bash
git commit -m "$(cat <<'EOF'
otel-semconv: scaffold three packages

- Qyl.OpenTelemetry.SemanticConventions (stable OTel attrs)
- Qyl.OpenTelemetry.SemanticConventions.Incubating (dev/experimental OTel attrs)
- Qyl.SemanticConventions (qyl custom attrs)
All three added to qyl.slnx. csharp_stable Weaver template confirmed:
http namespace generates HttpAttributes.g.cs with stable consts + RequestMethodValues.

Co-Authored-By: Claude Sonnet 4.6 <noreply@anthropic.com>
EOF
)"
```

---

## Phase 2 — Full Stable Template (All Namespaces + Deprecated + Reserved Check)

### Task 8: Verify all namespaces generate and build cleanly

**Files:** None (verification step)

- [ ] **Step 1: Count generated stable files**

After the Weaver run from Task 6 Step 2 (which already ran), check the output:

```bash
find packages/Qyl.OpenTelemetry.SemanticConventions/Attributes -name "*.g.cs" | wc -l
```

Expected: 40–60 files (only namespaces that have ≥1 stable attr produce a file).

- [ ] **Step 2: Verify reserved attrs are all present**

```bash
python3 - << 'EOF'
import os, glob, sys

reserved = [
    "error.type", "exception.message", "exception.stacktrace", "exception.type",
    "server.address", "server.port", "service.name", "service.instance.id",
    "telemetry.sdk.language", "telemetry.sdk.name", "telemetry.sdk.version",
    "url.scheme",
]

generated = glob.glob("packages/Qyl.OpenTelemetry.SemanticConventions/Attributes/**/*.g.cs", recursive=True)
content = "\n".join(open(f).read() for f in generated)

missing = [a for a in reserved if f'= "{a}"' not in content]
if missing:
    print("MISSING reserved attrs:", missing)
    sys.exit(1)
else:
    print("All reserved attrs present.")
EOF
```

Expected: `All reserved attrs present.`

If any are missing, add the failing namespace to the template's `selectattr("stability", "equalto", "stable")` check — the attr may be marked `development`. For `service.instance.id` check: it might be `experimental`. If so, include it explicitly by adding an `include_override` param in weaver.yaml and a `{% if attr.name in params.force_include_attrs %}` branch in the template.

- [ ] **Step 3: Full solution build check**

```bash
dotnet build packages/Qyl.OpenTelemetry.SemanticConventions/Qyl.OpenTelemetry.SemanticConventions.csproj --tl:off 2>&1 | tail -5
```

Expected: `Build succeeded. 0 Error(s)`

---

### Task 9: Copy deprecated-lookup files to eng/semconv/deprecated-lookup/

**Files:**
- Create: `eng/semconv/deprecated-lookup/semconv-v1.40.0-attributes.yaml`
- Create: `eng/semconv/deprecated-lookup/semconv-v1.40.0-metrics.yaml`
- Create: `eng/semconv/deprecated-lookup/semconv-v1.40.0-events.yaml`
- Create: `eng/semconv/deprecated-lookup/semconv-v1.40.0-enum-members.yaml`
- Create: `eng/semconv/deprecated-lookup/semconv-v1.40.0-entities.yaml`

Note: The Jinja templates use `attr.deprecated` (natively from Weaver) for `[Obsolete]` messages — no template file loading needed. These files serve as auditable reference data and as input for `bump-semconv` re-derivation.

- [ ] **Step 1: Create the directory and copy files**

```bash
mkdir -p eng/semconv/deprecated-lookup
cp ~/Documents/Codex/2026-04-22-spawn-18-codex-spark-agents-that/semconv_mapped_template/semconv-v1.40.0-attributes.yaml \
   eng/semconv/deprecated-lookup/
cp ~/Documents/Codex/2026-04-22-spawn-18-codex-spark-agents-that/semconv_mapped_template/semconv-v1.40.0-metrics.yaml \
   eng/semconv/deprecated-lookup/
cp ~/Documents/Codex/2026-04-22-spawn-18-codex-spark-agents-that/semconv_mapped_template/semconv-v1.40.0-events.yaml \
   eng/semconv/deprecated-lookup/
cp ~/Documents/Codex/2026-04-22-spawn-18-codex-spark-agents-that/semconv_mapped_template/semconv-v1.40.0-enum-members.yaml \
   eng/semconv/deprecated-lookup/ 2>/dev/null || \
cp ~/Documents/Codex/2026-04-22-spawn-18-codex-spark-agents-that/semconv_mapped_template/semconv-v1.40.0-enum-members.yaml \
   eng/semconv/deprecated-lookup/semconv-v1.40.0-enum-members.yaml 2>/dev/null || true
cp ~/Documents/Codex/2026-04-22-spawn-18-codex-spark-agents-that/semconv_mapped_template/semconv-v1.40.0-entities.yaml \
   eng/semconv/deprecated-lookup/
```

Note: The enum-members file may be named `semconv-v1.40.0-enum-members.yaml` — check the actual filename with `ls ~/Documents/Codex/2026-04-22-spawn-18-codex-spark-agents-that/semconv_mapped_template/`.

- [ ] **Step 2: Verify [Obsolete] annotations in generated output**

The template already emits `[System.Obsolete]` for attrs with `attr.deprecated` set. Spot-check:

```bash
grep -A1 "Obsolete" packages/Qyl.OpenTelemetry.SemanticConventions/Attributes/Http/HttpAttributes.g.cs | head -20
```

Expected: Lines like:
```
    [System.Obsolete("Replaced by http.request.method.", false)]
    public const string Method = "http.method";
```

---

### Task 10: Copy OTel schema file as embedded resource

**Files:**
- Create: `packages/Qyl.OpenTelemetry.SemanticConventions/schemas/1.40.0.yaml`
- Create: `packages/Qyl.OpenTelemetry.SemanticConventions.Incubating/schemas/1.40.0.yaml`

- [ ] **Step 1: Copy schema file from upstream**

```bash
cp .tools/semconv-upstream/schemas/1.40.0.yaml \
   packages/Qyl.OpenTelemetry.SemanticConventions/schemas/1.40.0.yaml
cp .tools/semconv-upstream/schemas/1.40.0.yaml \
   packages/Qyl.OpenTelemetry.SemanticConventions.Incubating/schemas/1.40.0.yaml
```

- [ ] **Step 2: Verify EmbeddedResource is picked up**

```bash
dotnet build packages/Qyl.OpenTelemetry.SemanticConventions/Qyl.OpenTelemetry.SemanticConventions.csproj --tl:off 2>&1 | grep -i "schema\|embed" | head -5
```

If the schema YAML isn't found (doesn't exist in upstream at that path), check:
```bash
ls .tools/semconv-upstream/schemas/
```

Upstream schemas are at `.tools/semconv-upstream/schemas/v1.40.0.yaml` (with a `v` prefix) or similar — adjust the copy command to match the actual filename.

---

### Task 11: Commit Phase 2

- [ ] **Step 1: Stage and commit**

```bash
git add eng/semconv/deprecated-lookup/ \
        packages/Qyl.OpenTelemetry.SemanticConventions/schemas/ \
        packages/Qyl.OpenTelemetry.SemanticConventions.Incubating/schemas/
git commit -m "$(cat <<'EOF'
otel-semconv: weaver templates stable

All stable OTel attrs generated. [Obsolete] annotations from native
attr.deprecated field (reason/renamed_to/note). Deprecated-lookup YAMLs
copied as reference data. Reserved attrs verified present. OTel schema
file embedded in both packages.

Co-Authored-By: Claude Sonnet 4.6 <noreply@anthropic.com>
EOF
)"
```

---

## Phase 3 — Incubating Template

### Task 12: Create csharp_incubating template set

**Files:**
- Create: `eng/semconv/templates/registry/csharp_incubating/weaver.yaml`
- Create: `eng/semconv/templates/registry/csharp_incubating/attributes.cs.j2`
- Create: `eng/semconv/templates/registry/csharp_incubating/schema_url.cs.j2`
- Create: `eng/semconv/templates/registry/csharp_incubating/schema_version.cs.j2`

- [ ] **Step 1: Write csharp_incubating/weaver.yaml**

Create `eng/semconv/templates/registry/csharp_incubating/weaver.yaml`:

```yaml
# Weaver template config for Qyl.OpenTelemetry.SemanticConventions.Incubating.
# Outputs stability: experimental + development attrs (everything that is NOT stable).

whitespace_control:
  trim_blocks: true
  lstrip_blocks: true

params:
  schema_url: "https://opentelemetry.io/schemas/1.40.0"
  schema_version: "1.40.0"
  semconv_commit: "7fe537301d17919af7d7eb65b32e9be35da2c497"
  package_namespace: "Qyl.OpenTelemetry.SemanticConventions.Incubating"

templates:
  - template: attributes.cs.j2
    filter: semconv_grouped_attributes
    application_mode: each
    file_name: "Attributes/{{ ctx.root_namespace | pascal_case }}/{{ ctx.root_namespace | pascal_case }}Attributes.g.cs"

  - template: schema_url.cs.j2
    filter: semconv_grouped_attributes
    application_mode: single
    file_name: "SchemaUrl.g.cs"

  - template: schema_version.cs.j2
    filter: semconv_grouped_attributes
    application_mode: single
    file_name: "SchemaVersion.g.cs"
```

- [ ] **Step 2: Write attributes.cs.j2 (incubating — filters non-stable)**

Create `eng/semconv/templates/registry/csharp_incubating/attributes.cs.j2`:

```jinja
{%- set incubating_attrs = ctx.attributes | rejectattr("stability", "equalto", "stable") | list -%}
{%- if incubating_attrs | length > 0 -%}
// <auto-generated/>
// Generated by qyl's Weaver pipeline from open-telemetry/semantic-conventions@{{ params.semconv_commit }}
// Schema: {{ params.schema_url }}
// Licensed under Apache-2.0 (inherited from OpenTelemetry upstream)
// WARNING: These attributes are experimental/development and may change in future versions.
// </auto-generated>

// Copyright (c) 2025-2026 ancplua

namespace {{ params.package_namespace }}.Attributes.{{ ctx.root_namespace | pascal_case }};

/// <summary>{{ ctx.display_name | default(ctx.root_namespace | pascal_case + " Attributes (Incubating)") }}.</summary>
/// <remarks>All attributes in this class are experimental or in development. They may be renamed or removed.</remarks>
public static class {{ ctx.root_namespace | pascal_case }}Attributes
{
{% for attr in incubating_attrs | sort(attribute="name") %}
    /// <summary>{{ attr.brief | replace('\n', ' ') | replace('"', "'") | trim }}.</summary>
{% if attr.note is defined and attr.note %}
    /// <remarks>{{ attr.note | replace('\n', ' ') | replace('"', "'") | truncate(500, True, '...') | trim }}</remarks>
{% endif %}
{% if attr.deprecated %}
{% if attr.deprecated.reason == "renamed" %}
    [System.Obsolete("Replaced by {{ attr.deprecated.renamed_to }}.", false)]
{% elif attr.deprecated.reason == "obsoleted" %}
    [System.Obsolete("Removed, no replacement.", false)]
{% else %}
    [System.Obsolete({{ attr.deprecated.note | replace('\n', ' ') | trim | tojson }}, false)]
{% endif %}
{% endif %}
    public const string {{ attr.name[(ctx.root_namespace | length + 1):] | replace(".", "_") | pascal_case }} = "{{ attr.name }}";
{% if attr.type is mapping and attr.type.members is defined %}

    /// <summary>Values for <see cref="{{ attr.name[(ctx.root_namespace | length + 1):] | replace(".", "_") | pascal_case }}"/>.</summary>
    public static class {{ attr.name[(ctx.root_namespace | length + 1):] | replace(".", "_") | pascal_case }}Values
    {
{% for member in attr.type.members | sort(attribute="value") %}
        /// <summary>{{ member.brief | replace('\n', ' ') | replace('"', "'") | trim }}.</summary>
{% if member.deprecated %}
        [System.Obsolete({{ member.deprecated | string | tojson }}, false)]
{% endif %}
        public const string {{ member.id | pascal_case }} = {{ member.value | tojson }};
{% endfor %}
    }
{% endif %}

{% endfor %}
}
{%- endif -%}
```

- [ ] **Step 3: Write schema_url.cs.j2 and schema_version.cs.j2 (copy from stable, change package_namespace)**

These are identical to the stable versions except the template uses `params.package_namespace` which is already set to the incubating value in weaver.yaml.

Copy `eng/semconv/templates/registry/csharp_stable/schema_url.cs.j2` → `csharp_incubating/schema_url.cs.j2`
Copy `eng/semconv/templates/registry/csharp_stable/schema_version.cs.j2` → `csharp_incubating/schema_version.cs.j2`

The templates reference `{{ params.package_namespace }}` so no edits needed.

- [ ] **Step 4: Run Weaver for incubating**

```bash
WEAVER_BIN=".tools/weaver/weaver-aarch64-apple-darwin/weaver"
"$WEAVER_BIN" registry generate \
  --registry .tools/semconv-upstream/model \
  --templates eng/semconv/templates/registry \
  csharp_incubating \
  packages/Qyl.OpenTelemetry.SemanticConventions.Incubating
```

Expected: more files than stable (incubating has more attrs — everything with `development` or `experimental` stability).

- [ ] **Step 5: Build incubating package**

```bash
dotnet build packages/Qyl.OpenTelemetry.SemanticConventions.Incubating/Qyl.OpenTelemetry.SemanticConventions.Incubating.csproj --tl:off 2>&1 | tail -3
```

Expected: `Build succeeded. 0 Error(s)`

---

### Task 13: Commit Phase 3

- [ ] **Step 1: Stage and commit**

```bash
git add eng/semconv/templates/registry/csharp_incubating/ \
        packages/Qyl.OpenTelemetry.SemanticConventions.Incubating/
git commit -m "$(cat <<'EOF'
otel-semconv: weaver templates incubating

csharp_incubating template mirrors stable but filters stability != "stable".
Incubating package builds cleanly.

Co-Authored-By: Claude Sonnet 4.6 <noreply@anthropic.com>
EOF
)"
```

---

## Phase 4 — QYL Custom Attributes

### Task 14: Scan codebase for qyl.* string literals to extract attribute names

**Files:** None (research step, informs qyl YAML content)

- [ ] **Step 1: Extract all qyl.* attribute strings used in services**

```bash
grep -r '"qyl\.' services/ packages/ --include="*.cs" -h \
  | grep -o '"qyl\.[^"]*"' | sort -u | tee /tmp/qyl-attrs-raw.txt
wc -l /tmp/qyl-attrs-raw.txt
```

Use this list to inform the YAML model content in Task 15. Group by first two segments (e.g., `qyl.capability.*`, `qyl.fix.*`, `qyl.run.*`).

---

### Task 15: Create qyl attribute group YAML models

**Files:**
- Create: `eng/semconv/qyl/model/capability.yaml`
- Create: `eng/semconv/qyl/model/run.yaml`
- Create: `eng/semconv/qyl/model/project.yaml`
- Create: `eng/semconv/qyl/model/team.yaml`
- Create: `eng/semconv/qyl/model/issue.yaml`
- Create: `eng/semconv/qyl/model/triage.yaml`
- Create: `eng/semconv/qyl/model/fix_run.yaml`
- Create: `eng/semconv/qyl/model/api_key.yaml`

All follow OTel's attribute_group YAML schema. Use `stability: stable` for all (no stability split in qyl package yet).

- [ ] **Step 1: Create eng/semconv/qyl/model/ directory**

```bash
mkdir -p eng/semconv/qyl/model
```

- [ ] **Step 2: Write capability.yaml**

Create `eng/semconv/qyl/model/capability.yaml`:

```yaml
groups:
  - id: registry.qyl.capability
    type: attribute_group
    display_name: qyl Capability Attributes
    brief: Attributes describing a qyl capability (the atomic unit of automation).
    attributes:
      - id: qyl.capability.id
        type: string
        stability: stable
        brief: Unique identifier of the capability.
        examples: ["cap_01hxk8mabcdef"]
      - id: qyl.capability.kind
        type:
          allow_custom_values: true
          members:
            - id: detector
              value: "detector"
              brief: A capability that detects issues.
              stability: stable
            - id: fixer
              value: "fixer"
              brief: A capability that applies fixes.
              stability: stable
            - id: triager
              value: "triager"
              brief: A capability that triages issues.
              stability: stable
        stability: stable
        brief: The category of capability.
```

- [ ] **Step 3: Write run.yaml**

Create `eng/semconv/qyl/model/run.yaml`:

```yaml
groups:
  - id: registry.qyl.run
    type: attribute_group
    display_name: qyl Agent Run Attributes
    brief: Attributes describing a qyl agent run.
    attributes:
      - id: qyl.run.id
        type: string
        stability: stable
        brief: Unique identifier of the agent run.
        examples: ["run_01hxk8mabcdef"]
      - id: qyl.run.status
        type:
          allow_custom_values: true
          members:
            - id: pending
              value: "pending"
              brief: The run is pending execution.
              stability: stable
            - id: running
              value: "running"
              brief: The run is currently executing.
              stability: stable
            - id: completed
              value: "completed"
              brief: The run completed successfully.
              stability: stable
            - id: failed
              value: "failed"
              brief: The run failed.
              stability: stable
        stability: stable
        brief: The execution status of the run.
      - id: qyl.run.agent_name
        type: string
        stability: stable
        brief: Name of the agent that executed this run.
        examples: ["loom.diagnostician", "loom.strategist"]
```

- [ ] **Step 4: Write project.yaml**

Create `eng/semconv/qyl/model/project.yaml`:

```yaml
groups:
  - id: registry.qyl.project
    type: attribute_group
    display_name: qyl Project Attributes
    brief: Attributes describing a qyl-observed project.
    attributes:
      - id: qyl.project.id
        type: string
        stability: stable
        brief: Unique identifier of the project.
        examples: ["proj_01hxk8mabcdef"]
      - id: qyl.project.name
        type: string
        stability: stable
        brief: Human-readable name of the project.
        examples: ["qyl", "my-service"]
      - id: qyl.project.repo_url
        type: string
        stability: stable
        brief: Repository URL of the project.
        examples: ["https://github.com/ancplua/qyl"]
```

- [ ] **Step 5: Write team.yaml**

Create `eng/semconv/qyl/model/team.yaml`:

```yaml
groups:
  - id: registry.qyl.team
    type: attribute_group
    display_name: qyl Team Attributes
    brief: Attributes describing a qyl team.
    attributes:
      - id: qyl.team.id
        type: string
        stability: stable
        brief: Unique identifier of the team.
        examples: ["team_01hxk8mabcdef"]
      - id: qyl.team.name
        type: string
        stability: stable
        brief: Human-readable name of the team.
        examples: ["platform", "backend"]
```

- [ ] **Step 6: Write issue.yaml**

Create `eng/semconv/qyl/model/issue.yaml`:

```yaml
groups:
  - id: registry.qyl.issue
    type: attribute_group
    display_name: qyl Issue Attributes
    brief: Attributes describing a qyl-tracked issue.
    attributes:
      - id: qyl.issue.id
        type: string
        stability: stable
        brief: Unique identifier of the issue.
        examples: ["iss_01hxk8mabcdef"]
      - id: qyl.issue.kind
        type:
          allow_custom_values: true
          members:
            - id: error
              value: "error"
              brief: A runtime error issue.
              stability: stable
            - id: regression
              value: "regression"
              brief: A performance regression.
              stability: stable
            - id: anomaly
              value: "anomaly"
              brief: An anomalous behavior detected.
              stability: stable
        stability: stable
        brief: The kind of issue.
      - id: qyl.issue.status
        type:
          allow_custom_values: true
          members:
            - id: open
              value: "open"
              brief: The issue is open.
              stability: stable
            - id: closed
              value: "closed"
              brief: The issue is closed.
              stability: stable
            - id: in_progress
              value: "in_progress"
              brief: A fix is in progress.
              stability: stable
        stability: stable
        brief: The status of the issue.
```

- [ ] **Step 7: Write triage.yaml**

Create `eng/semconv/qyl/model/triage.yaml`:

```yaml
groups:
  - id: registry.qyl.triage
    type: attribute_group
    display_name: qyl Triage Attributes
    brief: Attributes describing a qyl triage operation.
    attributes:
      - id: qyl.triage.id
        type: string
        stability: stable
        brief: Unique identifier of the triage record.
        examples: ["tri_01hxk8mabcdef"]
      - id: qyl.triage.score
        type: int
        stability: stable
        brief: Priority score assigned during triage (higher = more urgent).
        examples: [90, 50, 10]
      - id: qyl.triage.severity
        type:
          allow_custom_values: true
          members:
            - id: critical
              value: "critical"
              brief: Critical severity requiring immediate attention.
              stability: stable
            - id: high
              value: "high"
              brief: High severity.
              stability: stable
            - id: medium
              value: "medium"
              brief: Medium severity.
              stability: stable
            - id: low
              value: "low"
              brief: Low severity.
              stability: stable
        stability: stable
        brief: The severity level assigned during triage.
```

- [ ] **Step 8: Write fix_run.yaml**

Create `eng/semconv/qyl/model/fix_run.yaml`:

```yaml
groups:
  - id: registry.qyl.fix_run
    type: attribute_group
    display_name: qyl Fix Run Attributes
    brief: Attributes describing a qyl automated fix run.
    attributes:
      - id: qyl.fix_run.id
        type: string
        stability: stable
        brief: Unique identifier of the fix run.
        examples: ["fix_01hxk8mabcdef"]
      - id: qyl.fix_run.status
        type:
          allow_custom_values: true
          members:
            - id: pending
              value: "pending"
              brief: The fix run is pending.
              stability: stable
            - id: approved
              value: "approved"
              brief: The fix run has been approved.
              stability: stable
            - id: rejected
              value: "rejected"
              brief: The fix run was rejected.
              stability: stable
            - id: applied
              value: "applied"
              brief: The fix was applied.
              stability: stable
        stability: stable
        brief: The status of the fix run.
      - id: qyl.fix_run.plan
        type: string
        stability: stable
        brief: The fix plan description.
        examples: ["Add null check before accessing collection"]
      - id: qyl.fix_run.verification
        type: string
        stability: stable
        brief: Result of the fix verification step.
        examples: ["tests_pass", "build_error", "regression_detected"]
```

- [ ] **Step 9: Write api_key.yaml**

Create `eng/semconv/qyl/model/api_key.yaml`:

```yaml
groups:
  - id: registry.qyl.api_key
    type: attribute_group
    display_name: qyl API Key Attributes
    brief: Attributes describing a qyl API key used for authentication.
    attributes:
      - id: qyl.api_key.id
        type: string
        stability: stable
        brief: Unique identifier of the API key (non-secret prefix only).
        examples: ["qk_01hxk8m"]
      - id: qyl.api_key.name
        type: string
        stability: stable
        brief: Human-readable label for the API key.
        examples: ["ci-runner", "dashboard"]
      - id: qyl.api_key.created_by
        type: string
        stability: stable
        brief: User or service that created the API key.
        examples: ["user_abc", "service_ci"]
```

---

### Task 16: Create csharp_qyl template set

**Files:**
- Create: `eng/semconv/templates/registry/csharp_qyl/weaver.yaml`
- Create: `eng/semconv/templates/registry/csharp_qyl/attributes.cs.j2`
- Create: `eng/semconv/templates/registry/csharp_qyl/schema_url.cs.j2`

- [ ] **Step 1: Write csharp_qyl/weaver.yaml**

Create `eng/semconv/templates/registry/csharp_qyl/weaver.yaml`:

```yaml
# Weaver template config for Qyl.SemanticConventions (qyl custom attrs).
# Runs against eng/semconv/qyl/model/ (NOT upstream OTel model).
# No stability split yet — all qyl attrs are treated as stable.

whitespace_control:
  trim_blocks: true
  lstrip_blocks: true

params:
  schema_url: "https://schemas.qyl.io/1.0.0"
  schema_version: "1.0.0"
  package_namespace: "Qyl.SemanticConventions"

templates:
  - template: attributes.cs.j2
    filter: semconv_grouped_attributes
    application_mode: each
    file_name: "Attributes/{{ ctx.root_namespace | pascal_case }}/{{ ctx.root_namespace | pascal_case }}Attributes.g.cs"

  - template: schema_url.cs.j2
    filter: semconv_grouped_attributes
    application_mode: single
    file_name: "SchemaUrl.g.cs"
```

- [ ] **Step 2: Write csharp_qyl/attributes.cs.j2**

Create `eng/semconv/templates/registry/csharp_qyl/attributes.cs.j2`:

```jinja
{%- set all_attrs = ctx.attributes | list -%}
{%- if all_attrs | length > 0 -%}
// <auto-generated/>
// Generated by qyl's Weaver pipeline from qyl's own attribute registry
// Schema: {{ params.schema_url }}
// </auto-generated>

// Copyright (c) 2025-2026 ancplua

namespace {{ params.package_namespace }}.Attributes.{{ ctx.root_namespace | pascal_case }};

/// <summary>{{ ctx.display_name | default(ctx.root_namespace | pascal_case + " Attributes") }}.</summary>
public static class {{ ctx.root_namespace | pascal_case }}Attributes
{
{% for attr in all_attrs | sort(attribute="name") %}
    /// <summary>{{ attr.brief | replace('\n', ' ') | replace('"', "'") | trim }}.</summary>
    public const string {{ attr.name[(ctx.root_namespace | length + 1):] | replace(".", "_") | pascal_case }} = "{{ attr.name }}";
{% if attr.type is mapping and attr.type.members is defined %}

    /// <summary>Values for <see cref="{{ attr.name[(ctx.root_namespace | length + 1):] | replace(".", "_") | pascal_case }}"/>.</summary>
    public static class {{ attr.name[(ctx.root_namespace | length + 1):] | replace(".", "_") | pascal_case }}Values
    {
{% for member in attr.type.members | sort(attribute="value") %}
        /// <summary>{{ member.brief | replace('\n', ' ') | replace('"', "'") | trim }}.</summary>
        public const string {{ member.id | pascal_case }} = {{ member.value | tojson }};
{% endfor %}
    }
{% endif %}

{% endfor %}
}
{%- endif -%}
```

- [ ] **Step 3: Write csharp_qyl/schema_url.cs.j2**

Create `eng/semconv/templates/registry/csharp_qyl/schema_url.cs.j2`:

```jinja
// <auto-generated/>
// Generated by qyl's Weaver pipeline from qyl's own attribute registry
// Schema: {{ params.schema_url }}
// </auto-generated>

// Copyright (c) 2025-2026 ancplua

namespace {{ params.package_namespace }};

/// <summary>Schema URL for qyl Semantic Conventions {{ params.schema_version }}.</summary>
public static partial class SchemaUrl
{
    /// <summary>The schema URL for qyl semconv {{ params.schema_version }}.</summary>
    public const string Current = "{{ params.schema_url }}";
}
```

---

### Task 17: Create qyl schema file and run Weaver for qyl package

**Files:**
- Create: `eng/semconv/qyl/schemas/1.0.0.yaml`
- Create: `packages/Qyl.SemanticConventions/schemas/1.0.0.yaml`

- [ ] **Step 1: Create qyl schema file (minimal — no transformations yet)**

Create `eng/semconv/qyl/schemas/1.0.0.yaml`:

```yaml
# qyl Semantic Conventions schema file.
# Schema URL: https://schemas.qyl.io/1.0.0
# This schema defines transformation rules for telemetry migration.
# Currently empty — no attribute renames have occurred in qyl's schema history.
file_format: 1.1.0
schema_url: 'https://schemas.qyl.io/1.0.0'
versions:
  1.0.0:
    changes: []
```

- [ ] **Step 2: Copy schema to package**

```bash
mkdir -p eng/semconv/qyl/schemas
cp eng/semconv/qyl/schemas/1.0.0.yaml packages/Qyl.SemanticConventions/schemas/1.0.0.yaml
```

- [ ] **Step 3: Run Weaver for qyl package**

```bash
WEAVER_BIN=".tools/weaver/weaver-aarch64-apple-darwin/weaver"
"$WEAVER_BIN" registry generate \
  --registry eng/semconv/qyl/model \
  --templates eng/semconv/templates/registry \
  csharp_qyl \
  packages/Qyl.SemanticConventions
```

Expected: files like `packages/Qyl.SemanticConventions/Attributes/Qyl/QylAttributes.g.cs` — wait, the root_namespace for `qyl.capability.*` will be `qyl` (the first segment). That means ALL qyl attrs land in `QylAttributes.g.cs`. Check whether Weaver returns `root_namespace = "qyl"` or `"qyl.capability"`.

If root_namespace comes back as just "qyl" (all attrs collide into one file), the output is fine — one class `QylAttributes` with all nested classes. If sub-namespaces appear, even better.

- [ ] **Step 4: Build qyl package**

```bash
dotnet build packages/Qyl.SemanticConventions/Qyl.SemanticConventions.csproj --tl:off 2>&1 | tail -3
```

Expected: `Build succeeded. 0 Error(s)`

---

### Task 18: Commit Phase 4

- [ ] **Step 1: Stage and commit**

```bash
git add eng/semconv/qyl/ \
        eng/semconv/templates/registry/csharp_qyl/ \
        packages/Qyl.SemanticConventions/
git commit -m "$(cat <<'EOF'
otel-semconv: qyl custom attrs to YAML

8 attribute_group YAMLs under eng/semconv/qyl/model/ covering
capability, run, project, team, issue, triage, fix_run, api_key.
csharp_qyl Weaver template generates Qyl.SemanticConventions package.

Co-Authored-By: Claude Sonnet 4.6 <noreply@anthropic.com>
EOF
)"
```

---

## Phase 5 — NUKE Integration

### Task 19: Add GenerateSemconvCsharp target to NUKE build

**Files:**
- Modify: `eng/build/BuildPipeline.cs`
- Modify: `eng/semconv/run-weaver.sh`

- [ ] **Step 1: Add GenerateSemconvCsharp target to BuildPipeline.cs**

Open `eng/build/BuildPipeline.cs`. Add the following target after the existing `GenerateSemconv` target:

```csharp
Target GenerateSemconvCsharp => d => d
    .Description("Weaver → three NuGet packages: stable OTel, incubating OTel, qyl custom attrs")
    .OnlyWhenStatic(() => SemconvDirectory.DirectoryExists())
    .Executes(() =>
    {
        var weaverBin = GetWeaverBinaryPath();
        var upstreamModel = RootDirectory / ".tools" / "semconv-upstream" / "model";
        var qylModel = SemconvDirectory / "qyl" / "model";
        var templates = SemconvDirectory / "templates" / "registry";

        if (!upstreamModel.DirectoryExists())
            throw new DirectoryNotFoundException(
                $"semconv-upstream model not found at {upstreamModel}. " +
                "Run: git submodule update --init .tools/semconv-upstream");

        // Stable OTel attrs
        var stableOut = PackagesDirectory / "Qyl.OpenTelemetry.SemanticConventions";
        ProcessTasks.StartProcess(weaverBin,
            $"registry generate --registry \"{upstreamModel}\" --templates \"{templates}\" csharp_stable \"{stableOut}\"",
            logOutput: true).AssertZeroExitCode();

        // Incubating OTel attrs
        var incubatingOut = PackagesDirectory / "Qyl.OpenTelemetry.SemanticConventions.Incubating";
        ProcessTasks.StartProcess(weaverBin,
            $"registry generate --registry \"{upstreamModel}\" --templates \"{templates}\" csharp_incubating \"{incubatingOut}\"",
            logOutput: true).AssertZeroExitCode();

        // qyl custom attrs
        var qylOut = PackagesDirectory / "Qyl.SemanticConventions";
        ProcessTasks.StartProcess(weaverBin,
            $"registry generate --registry \"{qylModel}\" --templates \"{templates}\" csharp_qyl \"{qylOut}\"",
            logOutput: true).AssertZeroExitCode();

        Log.Information("Verifying reserved OTel attrs...");
        VerifyReservedAttrs(stableOut);
    });
```

Also add two default interface methods `GetWeaverBinaryPath()` and `VerifyReservedAttrs()` to the `IPipeline` interface body (non-static so they can access `RootDirectory` from `IHazSourcePaths`):

```csharp
string GetWeaverBinaryPath()
{
    var arch = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture;
    var os = OperatingSystem.IsMacOS() ? "apple-darwin" : "unknown-linux-gnu";
    var archStr = arch == System.Runtime.InteropServices.Architecture.Arm64 ? "aarch64" : "x86_64";
    var binName = $"weaver-{archStr}-{os}";
    var path = RootDirectory / ".tools" / "weaver" / binName / "weaver";
    if (!path.FileExists())
        throw new FileNotFoundException(
            $"Weaver binary not found: {path}. Run: bash eng/semconv/bootstrap-weaver.sh", path);
    return path;
}

void VerifyReservedAttrs(AbsolutePath stableOut)
{
    string[] reserved =
    [
        "error.type", "exception.message", "exception.stacktrace", "exception.type",
        "server.address", "server.port", "service.name", "service.instance.id",
        "telemetry.sdk.language", "telemetry.sdk.name", "telemetry.sdk.version",
        "url.scheme",
    ];

    var allContent = stableOut.GlobFiles("Attributes/**/*.g.cs")
        .Select(f => File.ReadAllText(f))
        .Aggregate("", (a, b) => a + b);

    var missing = reserved.Where(a => !allContent.Contains($"= \"{a}\"")).ToArray();
    if (missing.Length > 0)
        throw new Exception(
            $"Reserved OTel attrs missing from stable package: {string.Join(", ", missing)}");

    Log.Information("All {Count} reserved OTel attrs verified present.", reserved.Length);
}
```

These are default interface methods on `IPipeline` (C# 8+). `RootDirectory` resolves via `IHazSourcePaths` which `IPipeline` already extends. Call them as `GetWeaverBinaryPath()` and `VerifyReservedAttrs(stableOut)` inside the target lambda.

- [ ] **Step 2: Update the Generate target to depend on GenerateSemconvCsharp**

Find the existing `Generate` target in BuildPipeline.cs:

```csharp
Target Generate => d => d
    .Description("Regenerate ALL code from TypeSpec + Weaver")
    .DependsOn(TypeSpecCompile)
    .DependsOn(GenerateSemconv);
```

Change to:

```csharp
Target Generate => d => d
    .Description("Regenerate ALL code from TypeSpec + Weaver")
    .DependsOn(TypeSpecCompile)
    .DependsOn(GenerateSemconv)
    .DependsOn(GenerateSemconvCsharp);
```

- [ ] **Step 3: Update run-weaver.sh to include the three C# generation runs**

Append to `eng/semconv/run-weaver.sh` after the existing `install` commands:

```bash
# ── C# NuGet package generation ─────────────────────────────────────────────
STABLE_PKG="${REPO_ROOT}/packages/Qyl.OpenTelemetry.SemanticConventions"
INCUBATING_PKG="${REPO_ROOT}/packages/Qyl.OpenTelemetry.SemanticConventions.Incubating"
QYL_PKG="${REPO_ROOT}/packages/Qyl.SemanticConventions"
QYL_MODEL="${REPO_ROOT}/eng/semconv/qyl/model"

echo ""
echo "Generating C# packages..."

"${WEAVER_BIN}" registry generate \
  --registry "${UPSTREAM_REGISTRY}" \
  --templates "${TEMPLATES_ROOT}" \
  csharp_stable \
  "${STABLE_PKG}"

"${WEAVER_BIN}" registry generate \
  --registry "${UPSTREAM_REGISTRY}" \
  --templates "${TEMPLATES_ROOT}" \
  csharp_incubating \
  "${INCUBATING_PKG}"

"${WEAVER_BIN}" registry generate \
  --registry "${QYL_MODEL}" \
  --templates "${TEMPLATES_ROOT}" \
  csharp_qyl \
  "${QYL_PKG}"

echo ""
echo "C# packages updated:"
echo "  ${STABLE_PKG}/Attributes/ ($(find "${STABLE_PKG}/Attributes" -name '*.g.cs' | wc -l | tr -d ' ') files)"
echo "  ${INCUBATING_PKG}/Attributes/ ($(find "${INCUBATING_PKG}/Attributes" -name '*.g.cs' | wc -l | tr -d ' ') files)"
echo "  ${QYL_PKG}/Attributes/ ($(find "${QYL_PKG}/Attributes" -name '*.g.cs' | wc -l | tr -d ' ') files)"
```

- [ ] **Step 4: Verify NUKE target builds**

```bash
dotnet build eng/build/ --tl:off 2>&1 | tail -5
```

Expected: NUKE build project compiles. If `WeaverBinaryPath` or `VerifyReservedAttrs` have compilation errors, fix them before proceeding.

- [ ] **Step 5: Run nuke GenerateSemconvCsharp**

```bash
nuke GenerateSemconvCsharp 2>&1 | tee /tmp/nuke-gen-csharp.log
grep -E "error|Error|✔|✗" /tmp/nuke-gen-csharp.log | head -20
```

Expected: all three Weaver runs succeed, reserved attrs verified.

---

### Task 20: Verify idempotency

- [ ] **Step 1: Run GenerateSemconvCsharp twice, check git status**

```bash
nuke GenerateSemconvCsharp
git status --short packages/Qyl.OpenTelemetry.SemanticConventions/ \
                    packages/Qyl.OpenTelemetry.SemanticConventions.Incubating/ \
                    packages/Qyl.SemanticConventions/
```

Expected: no output (git status clean after second run — files unchanged).

If files show as modified on second run, Weaver is generating non-deterministic output (e.g., timestamp-based headers). Fix: remove any `{{ now() }}` or timestamp in templates; use `{{ params.semconv_commit }}` instead.

---

### Task 21: Add Makefile bump-semconv target

**Files:**
- Create or modify: `Makefile`

- [ ] **Step 1: Create/update Makefile with bump-semconv target**

```makefile
.PHONY: bump-semconv
## Bump OTel semconv version. Usage: make bump-semconv VERSION=1.41.0
bump-semconv:
	@if [ -z "$(VERSION)" ]; then echo "Usage: make bump-semconv VERSION=X.Y.Z" && exit 1; fi
	@echo "Bumping semconv to v$(VERSION)..."
	git -C .tools/semconv-upstream fetch --tags
	git -C .tools/semconv-upstream checkout v$(VERSION)
	git add .tools/semconv-upstream
	# Update version strings in weaver.yaml files
	NEW_COMMIT=$$(git -C .tools/semconv-upstream rev-parse HEAD); \
	NEW_URL="https://opentelemetry.io/schemas/$(VERSION)"; \
	for f in eng/semconv/templates/registry/csharp_stable/weaver.yaml \
	          eng/semconv/templates/registry/csharp_incubating/weaver.yaml; do \
	  sed -i '' \
	    -e "s|schema_url: \".*\"|schema_url: \"$$NEW_URL\"|" \
	    -e "s|schema_version: \".*\"|schema_version: \"$(VERSION)\"|" \
	    -e "s|semconv_commit: \".*\"|semconv_commit: \"$$NEW_COMMIT\"|" \
	    $$f; \
	done
	# Re-derive deprecated-lookup YAMLs from new upstream
	python3 eng/semconv/derive-deprecated-lookup.py $(VERSION)
	# Regenerate all C# packages
	nuke GenerateSemconvCsharp
	# Copy updated schema file
	ls .tools/semconv-upstream/schemas/ | grep "$(VERSION)" | head -1 | \
	  xargs -I{} cp .tools/semconv-upstream/schemas/{} \
	    packages/Qyl.OpenTelemetry.SemanticConventions/schemas/$(VERSION).yaml
	cp packages/Qyl.OpenTelemetry.SemanticConventions/schemas/$(VERSION).yaml \
	   packages/Qyl.OpenTelemetry.SemanticConventions.Incubating/schemas/$(VERSION).yaml
	@echo "Done. Review changes, then commit:"
	@echo "  git commit -m 'chore(semconv): bump to v$(VERSION)'"
```

- [ ] **Step 2: Create derive-deprecated-lookup.py script**

Create `eng/semconv/derive-deprecated-lookup.py`:

```python
#!/usr/bin/env python3
"""Re-derive deprecated-lookup YAMLs from a fresh semconv checkout.
Usage: python3 eng/semconv/derive-deprecated-lookup.py 1.41.0
"""
import sys, glob, yaml, collections, os

version = sys.argv[1] if len(sys.argv) > 1 else "1.40.0"
repo_root = os.path.join(os.path.dirname(__file__), "../..")
model_root = os.path.join(repo_root, ".tools/semconv-upstream/model")
out_dir = os.path.join(repo_root, "eng/semconv/deprecated-lookup")
os.makedirs(out_dir, exist_ok=True)

entries = []
for f in sorted(glob.glob(f"{model_root}/**/*.yaml", recursive=True)):
    try:
        doc = yaml.safe_load(open(f))
    except Exception:
        continue
    if not doc or "groups" not in doc:
        continue
    for group in doc["groups"]:
        for attr in group.get("attributes", []):
            dep = attr.get("deprecated")
            if not dep:
                continue
            entry = {
                "deprecated_id": attr.get("id", ""),
                "status": dep.get("reason", "uncategorized"),
                "replacements": [dep["renamed_to"]] if dep.get("renamed_to") else [],
                "resolution_text": dep.get("note", ""),
                "replacement_mode": "direct" if dep.get("reason") == "renamed" else "removed",
            }
            entries.append(entry)

out = {"schema_version": 1, "dataset": f"semconv-v{version}-attributes",
       "counts": {"total_entries": len(entries)}, "entries": entries}
out_path = os.path.join(out_dir, f"semconv-v{version}-attributes.yaml")
yaml.dump(out, open(out_path, "w"), default_flow_style=False, allow_unicode=True)
print(f"Wrote {len(entries)} deprecated attrs to {out_path}")
```

---

### Task 22: Commit Phase 5

- [ ] **Step 1: Stage and commit**

```bash
git add eng/build/BuildPipeline.cs \
        eng/semconv/run-weaver.sh \
        Makefile \
        eng/semconv/derive-deprecated-lookup.py
git commit -m "$(cat <<'EOF'
otel-semconv: nuke integration

GenerateSemconvCsharp target runs all three Weaver passes.
Generate target now depends on GenerateSemconvCsharp.
Idempotency verified. bump-semconv Makefile target + derive script added.

Co-Authored-By: Claude Sonnet 4.6 <noreply@anthropic.com>
EOF
)"
```

---

## Phase 6 — Runtime Cutover

### Task 23: Identify all qyl.* magic string usages in C#

- [ ] **Step 1: Catalogue usages by package**

```bash
grep -r '"qyl\.' services/ packages/ --include="*.cs" -rn \
  | grep -v "\.g\.cs:" \
  | grep -E '"qyl\.(capability|run|project|team|issue|triage|fix_run|api_key)\.' \
  | tee /tmp/qyl-custom-usages.txt
wc -l /tmp/qyl-custom-usages.txt
```

```bash
grep -r '"qyl\.' services/ packages/ --include="*.cs" -rn \
  | grep -v "\.g\.cs:" \
  | grep -Ev '"qyl\.(capability|run|project|team|issue|triage|fix_run|api_key)\.' \
  | tee /tmp/qyl-misc-usages.txt
wc -l /tmp/qyl-misc-usages.txt
```

The first set maps to `Qyl.SemanticConventions.Attributes.*`. The second set (tool names, activity source names, etc.) should remain as magic strings — not all `"qyl.*"` literals are telemetry attribute keys.

- [ ] **Step 2: Identify which are telemetry attribute keys vs. other identifiers**

Review `/tmp/qyl-misc-usages.txt`. Filter to only lines that are used as `ActivityEvent`, `tag.Set`, `ActivityTagsCollection`, `AddTag`, span attribute keys, etc. The ones used as `ActivitySource` names (`"qyl.genai"`, `"qyl.agent"`) or tool names are NOT attribute keys — leave those as-is.

---

### Task 24: Replace OTel stable magic strings (GenAi, Http, Db, Mcp)

**Files:**
- Modify: `packages/Qyl.Contracts/Attributes/GenAiAttributes.cs` (will be deleted at end)
- Modify: all callers from grep results

For attrs that come from the stable OTel package (`gen_ai.*`, `http.*`, `db.*`, `mcp.*`):

- [ ] **Step 1: Add package reference to projects that use OTel attrs**

For each project that needs the new constants, add to its `.csproj`:

```xml
<PackageReference Include="Qyl.OpenTelemetry.SemanticConventions" />
```

Or for incubating attrs:
```xml
<PackageReference Include="Qyl.OpenTelemetry.SemanticConventions.Incubating" />
```

Since these are in-repo packages, they need to be referenced as `<ProjectReference>` not `<PackageReference>`:

```xml
<ProjectReference Include="$(PackagesDirectory)\Qyl.OpenTelemetry.SemanticConventions\Qyl.OpenTelemetry.SemanticConventions.csproj" />
```

- [ ] **Step 2: Replace "gen_ai.*" literals in qyl.instrumentation**

Replace each magic string with the typed constant. Example replacements:

```csharp
// Before
tags.Add("gen_ai.operation.name", operationName);

// After
using Qyl.OpenTelemetry.SemanticConventions.Attributes.GenAi;
tags.Add(GenAiAttributes.OperationName, operationName);
```

Run build after each file change:
```bash
dotnet build services/qyl.instrumentation/qyl.instrumentation.csproj --tl:off 2>&1 | tail -3
```

- [ ] **Step 3: Replace "db.*" literals in qyl.collector**

Same pattern. The `Db` namespace is in the incubating package (db attrs are experimental):

```csharp
using Qyl.OpenTelemetry.SemanticConventions.Incubating.Attributes.Db;
// "db.system" → DbAttributes.System
```

- [ ] **Step 4: Delete hand-written attribute files after callers are updated**

Once ALL callers in C# have been migrated and the build is clean:

```bash
rm packages/Qyl.Contracts/Attributes/GenAiAttributes.cs
rm packages/Qyl.Contracts/Attributes/DbAttributes.cs
rm packages/Qyl.Contracts/Attributes/McpAttributes.cs
dotnet build packages/Qyl.Contracts/qyl.contracts.csproj --tl:off 2>&1 | tail -3
```

---

### Task 25: Replace qyl.capability.*, qyl.run.*, qyl.issue.* etc. with Qyl.SemanticConventions

- [ ] **Step 1: Add ProjectReference to consumers**

For each service that uses `qyl.capability.*` or similar custom attrs, add:

```xml
<ProjectReference Include="$(PackagesDirectory)\Qyl.SemanticConventions\Qyl.SemanticConventions.csproj" />
```

- [ ] **Step 2: Replace literals**

```csharp
// Before
span.SetTag("qyl.capability.id", capabilityId);

// After
using Qyl.SemanticConventions.Attributes.Qyl; // or whatever root_namespace Weaver produced
span.SetTag(QylAttributes.CapabilityId, capabilityId);
```

- [ ] **Step 3: Build all services after cutover**

```bash
dotnet build services/qyl.collector/qyl.collector.csproj --tl:off 2>&1 | tail -5
dotnet build services/qyl.loom/qyl.loom.csproj --tl:off 2>&1 | tail -5
dotnet build services/qyl.mcp/qyl.mcp.csproj --tl:off 2>&1 | tail -5
```

---

### Task 26: Commit Phase 6

- [ ] **Step 1: Stage and commit**

```bash
git add -A
git commit -m "$(cat <<'EOF'
otel-semconv: runtime cutover from magic strings

Replace all telemetry attribute key literals with typed constants
from generated packages. Delete GenAiAttributes.cs, DbAttributes.cs,
McpAttributes.cs from Qyl.Contracts/Attributes/ (superseded).

Co-Authored-By: Claude Sonnet 4.6 <noreply@anthropic.com>
EOF
)"
```

---

## Phase 7 — Cleanup

### Task 27: Shrink qyl-semconv-lint to one diagnostic

**Files:**
- Modify: `core/specs/emitters/qyl-semconv-lint/` (TypeSpec emitter)

- [ ] **Step 1: Find existing diagnostics in lint emitter**

```bash
find core/specs/emitters/qyl-semconv-lint -name "*.ts" | xargs grep -l "diagnostic\|Diagnostic" 2>/dev/null
```

- [ ] **Step 2: Delete 5 of the 6 diagnostics, keep only the overlap check**

The only diagnostic to keep: "no qyl.* key in TSP overlaps with registry" — checks that TypeSpec models don't accidentally reuse attribute names that are already in the qyl registry.

Delete (or comment out and remove from exports):
- Any lint rule that checks OTel semconv conformance (now handled by generated constants)
- Any lint rule that validates qyl attr naming conventions
- Any lint rule that enforces attribute prefix format

Keep:
- The `noQylAttrOverlapWithRegistry` check (or equivalent name)

Build the TypeSpec project after to confirm no compile errors:
```bash
cd core/specs && npm run compile 2>&1 | tail -10
```

---

### Task 28: Remove TypeSpec devDeps no longer needed

**Files:**
- Modify: `core/specs/package.json`

- [ ] **Step 1: Remove obsolete devDependencies**

Remove from `core/specs/package.json` devDependencies:
- `@typespec/events`
- `@typespec/openapi`
- `@typespec/openapi3`
- `@typespec/sse`
- `@typespec/streams`
- `@typespec/versioning`
- `openapi-typescript`

**IMPORTANT**: Verify each is NOT used by TypeSpec Pipeline 2 (API shapes, routes, models) before removing. Search:
```bash
grep -r "@typespec/events\|@typespec/sse\|@typespec/streams\|@typespec/versioning" core/specs/ --include="*.tsp" | head -20
```

Only remove those with zero usage.

- [ ] **Step 2: Remove qyl-attrs.tsp**

```bash
grep -r "qyl-attrs\|qylAttr" core/specs/ --include="*.tsp" | head -10
```

If `core/specs/telemetry/qyl-attrs.tsp` exists and its content has been superseded by the generated Qyl.SemanticConventions package, delete it:

```bash
rm -f core/specs/telemetry/qyl-attrs.tsp
cd core/specs && npm run compile 2>&1 | tail -5
```

- [ ] **Step 3: npm install and build to confirm**

```bash
cd core/specs && npm install --legacy-peer-deps && npm run compile 2>&1 | tail -5
```

---

### Task 29: Commit Phase 7

- [ ] **Step 1: Stage and commit**

```bash
git add core/specs/ -u
git commit -m "$(cat <<'EOF'
otel-semconv: shrink lint lib

qyl-semconv-lint reduced to single diagnostic: qyl-registry overlap check.
Removed 5 rules superseded by generated package type safety.
Removed TypeSpec devDeps no longer used post-Weaver pipeline.
Removed qyl-attrs.tsp (superseded by Qyl.SemanticConventions package).

Co-Authored-By: Claude Sonnet 4.6 <noreply@anthropic.com>
EOF
)"
```

---

## Phase 8 — Merge Gate

### Task 30: Full build, test, pack, and npm verify

- [ ] **Step 1: Full solution build**

```bash
dotnet build qyl.slnx --tl:off 2>&1 | tee /tmp/merge-gate-build.log
grep -E "^.*error" /tmp/merge-gate-build.log | head -20
```

Expected: `Build succeeded. 0 Error(s)`.

- [ ] **Step 2: Tests**

```bash
dotnet test --project tests/qyl.collector.tests --tl:off 2>&1 | tail -10
```

Expected: all tests pass.

- [ ] **Step 3: Pack all three new packages**

```bash
dotnet pack packages/Qyl.OpenTelemetry.SemanticConventions/Qyl.OpenTelemetry.SemanticConventions.csproj \
  --output /tmp/semconv-pack --tl:off 2>&1 | tail -5
dotnet pack packages/Qyl.OpenTelemetry.SemanticConventions.Incubating/Qyl.OpenTelemetry.SemanticConventions.Incubating.csproj \
  --output /tmp/semconv-pack --tl:off 2>&1 | tail -5
dotnet pack packages/Qyl.SemanticConventions/Qyl.SemanticConventions.csproj \
  --output /tmp/semconv-pack --tl:off 2>&1 | tail -5
ls /tmp/semconv-pack/
```

Expected: three `.nupkg` files present.

- [ ] **Step 4: npm build (dashboard)**

```bash
cd services/qyl.dashboard && npm run build 2>&1 | tail -10
```

Expected: build succeeds.

- [ ] **Step 5: nuke Generate idempotency final check**

```bash
nuke GenerateSemconvCsharp
git status --short packages/Qyl.OpenTelemetry.SemanticConventions/ \
                    packages/Qyl.OpenTelemetry.SemanticConventions.Incubating/ \
                    packages/Qyl.SemanticConventions/
```

Expected: no modified files.

---

### Task 31: Commit merge gate

- [ ] **Step 1: Final commit**

```bash
git commit -m "$(cat <<'EOF'
otel-semconv: merge gate green

dotnet build clean, tests pass, all three packages pack successfully,
npm build passes, GenerateSemconvCsharp idempotent.

Co-Authored-By: Claude Sonnet 4.6 <noreply@anthropic.com>
EOF
)" --allow-empty
```

---

## Appendix: Key Weaver Template Facts

Discovered during plan research (do not re-test during implementation):

- `semconv_grouped_attributes` returns **one group per `root_namespace`** (Weaver aggregates all `attribute_group` objects by root namespace). `application_mode: each` produces one file per namespace safely.
- `attr.deprecated` is a structured object `{note, reason, renamed_to}` or `null`. `reason` values: `"renamed"`, `"obsoleted"`, `"uncategorized"`. No external lookup file needed in templates.
- `attr.type.members` is available on enum-type attrs. Members have `id`, `value`, `stability`, `deprecated` fields.
- String slicing works in Weaver's Jinja2: `attr.name[(ctx.root_namespace | length + 1):]` correctly strips the root namespace prefix.
- `replace(old, new, count)` does NOT work — 3-argument replace is unsupported. Use `replace(old, new)` only.
- The `semconv_commit` for v1.40.0 is `7fe537301d17919af7d7eb65b32e9be35da2c497`.
