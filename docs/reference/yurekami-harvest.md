# yurekami harvest — routing stub

> The 2026-07-08 extraction sweep of 11 donor projects (~143k LOC) is **closed**:
> 1 pattern landed (GenAI cache/reasoning token cost), everything else was
> REF-ONLY. Per the rule *a reference is for what's missing*, the per-project
> dossiers and the resolved routing rows were deleted from HEAD on 2026-07-11 —
> the full table with per-row evidence lives at git `8c49a3ab`
> (`docs/reference/yurekami-extraction/` + this file), and the donor **source**
> is archived source-only in the private repo **`ANcpLua/yurekami-refs`**
> (`gh repo clone ANcpLua/yurekami-refs`; restore-verified 2026-07-08: HEAD
> `14e393e`, 1078 files, `git fsck` clean).

## Open rows (the only planned consumer: the qyl.mcp eval harness)

The qyl.mcp merge left an eval-harness seam (the exported catalog def list —
`qyl-workspace/qyl.mcp`, MCP-STRATEGY parity item 5). When that ships, these two
patterns are the liftable prior art — then **delete this file**:

| Pattern | Donor (in yurekami-refs) | Use |
|---|---|---|
| **Rubric-as-weighted-predicate autograder** — sum `(predicate, points, label)`, grep concepts not strings | claude-code-challenges; aegis `@grader`/`GraderRegistry` | deterministic eval/CI gate over agent output |
| **Verbalized Sampling** — default→forbid→T-scored alternatives→pick lowest-T meeting quality | anti-sameness-plugin skills | eval rubrics / anti-mode-collapse on any generation surface |

## Rule

Transpile into qyl idiom behind the existing verify gates, commit, **then delete
the row** — implemented patterns don't keep reference rows; git history keeps
the evidence.
