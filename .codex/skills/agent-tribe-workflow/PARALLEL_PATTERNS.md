# Parallel Execution Patterns Reference

## Pattern Library for Multi-Agent Orchestration

### Pattern 1: Comprehensive Agent Audit

**Use when**: Optimizing a single agent definition

**Subagent deployment** (5 parallel):

```
Task 1 (Explore, background):
"Analyze the agent's ARCHITECTURE: structure, dependencies, composition patterns.
Return: 3-5 bullet findings with specific line references."

Task 2 (Explore, background):
"Audit the agent's DESCRIPTION: trigger specificity, discoverability, key terms.
Return: Assessment of when Claude would/wouldn't invoke this agent."

Task 3 (Explore, background):
"Review TOKEN EFFICIENCY: context usage, instruction length, redundancy.
Return: Token count estimate and optimization opportunities."

Task 4 (Explore, background):
"Examine RELIABILITY: error handling patterns, edge cases, recovery paths.
Return: List of failure modes and missing safeguards."

Task 5 (Explore, background):
"Assess DOCUMENTATION: clarity, completeness, example quality.
Return: Gaps in guidance and ambiguous instructions."
```

**Synthesis**: Collect all 5 findings, identify conflicts between voices, resolve into unified plan.

---

### Pattern 2: Skill Purification

**Use when**: Optimizing a SKILL.md file

**Subagent deployment** (4 parallel):

```
Task 1 (Explore, background):
"Validate METADATA: name format (<64 chars, lowercase, hyphens only),
description format (<1024 chars, third person, includes when-to-use).
Return: Compliance checklist with specific violations."

Task 2 (Explore, background):
"Analyze STRUCTURE: body line count, file organization, reference depth.
Return: Structural assessment against <500 line guideline."

Task 3 (Explore, background):
"Review CONTENT: workflow clarity, example concreteness, terminology consistency.
Return: Content quality score with specific improvement targets."

Task 4 (Explore, background):
"Check INTEGRATION: tool references, MCP interactions, skill composition.
Return: Integration surface analysis and potential conflicts."
```

---

### Pattern 3: Multi-Agent System Optimization

**Use when**: Optimizing a system of 3+ interacting agents

**Subagent deployment** (3 per agent, in waves):

Wave 1 - Per-agent analysis:
```
For each agent, spawn 3 parallel subagents:
- Description/trigger analysis
- Instruction quality review
- Output/deliverable assessment
```

Wave 2 - Cross-agent analysis:
```
3 parallel subagents examining:
- Overlap detection (do agents duplicate functionality?)
- Gap analysis (are there uncovered scenarios?)
- Interaction patterns (how do agents hand off?)
```

Wave 3 - Synthesis:
```
Single subagent synthesizing all findings into:
- Unified optimization plan
- Breaking changes manifest
- Migration strategy
```

---

### Pattern 4: Plugin Deep Dive

**Use when**: Auditing Claude Code plugin architecture

**Subagent deployment** (4 parallel):

```
Task 1: "Analyze COMMANDS: definition quality, trigger patterns, argument handling"
Task 2: "Review HOOKS: event handling, side effects, error propagation"
Task 3: "Assess RESOURCES: bundled content efficiency, loading patterns"
Task 4: "Check DEPENDENCIES: external requirements, version constraints, compatibility"
```

---

### Pattern 5: Codebase-Wide Skill Audit

**Use when**: Auditing all skills in ~/.claude/skills/

**Phased subagent deployment**:

Phase 1 - Discovery:
```
Single Explore subagent: "List all SKILL.md files, extract metadata, identify skill count"
```

Phase 2 - Parallel analysis (batch of 5):
```
For each batch of 5 skills, spawn 5 parallel subagents (1 per skill):
"Audit [skill_name]: metadata compliance, structure, content quality. Return score 0-100."
```

Phase 3 - Priority synthesis:
```
Single subagent: "Rank skills by optimization priority based on audit scores"
```

Phase 4 - Targeted optimization:
```
Use Pattern 2 (Skill Purification) on top 3 lowest-scoring skills
```

---

## Subagent Task Template

Every subagent task should include:

```
OBJECTIVE: [Specific dimension to investigate]

SCOPE: [What to include/exclude]

TOOLS: [Which tools to use: Glob, Grep, Read, etc.]

OUTPUT FORMAT:
- Finding 1: [description] (line X-Y)
- Finding 2: [description] (line X-Y)
- ...
- Recommendation: [specific action]

CONSTRAINTS:
- Return compressed findings (<2000 tokens)
- Include specific file/line references
- Do NOT investigate [other dimensions - avoid duplication]
```

---

## Scaling Guidelines

| Complexity | Artifacts | Subagents | Pattern |
|------------|-----------|-----------|---------|
| Simple | 1 agent | 3-5 | Single wave |
| Medium | 2-3 agents | 6-9 | Two waves |
| Complex | 4-5 agents | 12-15 | Three waves |
| System-wide | 5+ agents | 15-20 | Phased batches |

**Rule of thumb**: 3 subagents per artifact for comprehensive coverage.

---

## Error Recovery

If a subagent fails or returns insufficient data:

1. **Retry with refined prompt**: Add more specific guidance
2. **Decompose further**: Split the task into smaller pieces
3. **Escalate to lead**: Have lead agent investigate directly
4. **Document gap**: Note the limitation in synthesis

Never let a single subagent failure block the entire optimization.
