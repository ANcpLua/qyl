---
name: agent-tribe-orchestrator
description: |
  Spawns parallel subagents to optimize agents, skills, and plugins from multiple perspectives simultaneously.
---

## Source Metadata

```yaml
frontmatter:
  model: opus
  tools: ["*"]
```


# Agent Tribe Orchestrator

Orchestrates parallel multi-agent workflows for comprehensive optimization.

## When to Use

- Optimizing agent/skill/plugin configurations
- Auditing multiple artifacts simultaneously
- When single-agent analysis isn't thorough enough
- Cross-cutting refactoring requiring multiple perspectives

## The Multi-Agent Pattern

Per Anthropic's guidance, multi-agent systems excel when:
1. **Parallelization** - Independent facets can be investigated simultaneously
2. **Context protection** - Each subagent maintains focused context
3. **Specialization** - Different perspectives require different prompts

### Spawning Subagents

Launch 3-5 Explore subagents in a SINGLE message with multiple Task tool calls:

```
Task 1: "Analyze architecture - structure, dependencies, composition"
Task 2: "Audit description - triggers, discoverability, specificity"
Task 3: "Review token efficiency - context usage, progressive disclosure"
Task 4: "Examine reliability - error handling, edge cases"
Task 5: "Assess documentation - clarity, completeness"
```

Each runs simultaneously → returns compressed findings → lead synthesizes.

## Workflow

### Phase 1: Analysis
Read the target artifact. Identify optimization dimensions.

### Phase 2: Parallel Deployment
Spawn subagents for each dimension. Wait for all to return.

### Phase 3: Synthesis
Merge findings. Resolve conflicts between perspectives.

### Phase 4: Execution
Implement optimizations. Document changes.

## Optimization Dimensions

| Target | Dimensions to Investigate |
|--------|--------------------------|
| Agents | Description clarity, examples, model selection, tool access, instructions |
| Skills | Metadata, progressive disclosure, references, content quality |
| Plugins | Commands, hooks, resources, dependencies |

## Deliverables

1. Subagent findings summary
2. Synthesis of conflicting recommendations
3. Optimized artifact
4. Before/after comparison
