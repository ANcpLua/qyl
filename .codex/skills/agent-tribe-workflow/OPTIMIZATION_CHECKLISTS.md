# Optimization Checklists

## The Tribe's Verification Standards

Copy the relevant checklist and track progress as you purify.

---

## Agent Definition Checklist

```
Agent Purification Progress:
- [ ] DESCRIPTION
  - [ ] Specific enough for Claude to know WHEN to invoke
  - [ ] Includes key trigger terms and contexts
  - [ ] Under 1024 characters (system limit)
  - [ ] Written in third person (injected into system prompt)

- [ ] EXAMPLES (minimum 3-4)
  - [ ] Each has: Context, User message, Assistant response, Commentary
  - [ ] Examples cover different trigger scenarios
  - [ ] Commentary explains WHY the agent applies

- [ ] MODEL SELECTION
  - [ ] haiku: Simple, fast tasks
  - [ ] sonnet: Balanced complexity
  - [ ] opus: Complex reasoning, creative work

- [ ] TOOL ACCESS
  - [ ] Minimal necessary tools specified
  - [ ] NOT blanket ["*"] access unless truly needed
  - [ ] Tool descriptions guide proper usage

- [ ] INSTRUCTIONS
  - [ ] Right altitude (not too brittle, not too vague)
  - [ ] Clear actionable guidance
  - [ ] No assumed shared context

- [ ] DELIVERABLES
  - [ ] Explicit output requirements
  - [ ] Verification criteria specified
  - [ ] Success conditions defined

- [ ] TONE/STYLE
  - [ ] Consistent voice throughout
  - [ ] Appropriate for domain
  - [ ] No conflicting personas
```

---

## Skill (SKILL.md) Checklist

```
Skill Purification Progress:
- [ ] METADATA: name
  - [ ] Maximum 64 characters
  - [ ] Lowercase letters, numbers, hyphens only
  - [ ] No XML tags
  - [ ] No reserved words ("anthropic", "claude")
  - [ ] Gerund form preferred (e.g., "processing-pdfs")

- [ ] METADATA: description
  - [ ] Non-empty
  - [ ] Maximum 1024 characters
  - [ ] No XML tags
  - [ ] Written in THIRD PERSON
  - [ ] Includes WHAT the skill does
  - [ ] Includes WHEN to use it
  - [ ] Contains key trigger terms

- [ ] STRUCTURE
  - [ ] Body under 500 lines
  - [ ] Large content in separate files
  - [ ] File references one level deep
  - [ ] Table of contents for files >100 lines

- [ ] CONTENT
  - [ ] Consistent terminology (one term per concept)
  - [ ] Concrete examples (input/output pairs)
  - [ ] Clear workflows with checkable steps
  - [ ] No time-sensitive information
  - [ ] Old patterns in collapsible sections

- [ ] PROGRESSIVE DISCLOSURE
  - [ ] SKILL.md loads when triggered (~5k tokens)
  - [ ] Additional files load as needed
  - [ ] Scripts execute without loading code
  - [ ] No unnecessary upfront loading

- [ ] CODE/SCRIPTS (if applicable)
  - [ ] Scripts solve problems (don't punt to Claude)
  - [ ] Explicit error handling
  - [ ] No magic numbers (all values justified)
  - [ ] Forward slashes in paths (cross-platform)
  - [ ] Dependencies documented and available
```

---

## Plugin Checklist

```
Plugin Purification Progress:
- [ ] COMMANDS
  - [ ] Clear command definitions
  - [ ] Appropriate trigger patterns
  - [ ] Argument validation
  - [ ] Help text for each command

- [ ] HOOKS
  - [ ] Correct event handling
  - [ ] Side effects managed
  - [ ] Error propagation handled
  - [ ] Hook order considered

- [ ] RESOURCES
  - [ ] Bundled content necessary
  - [ ] Token-efficient organization
  - [ ] Progressive loading where applicable

- [ ] DEPENDENCIES
  - [ ] External requirements documented
  - [ ] Version constraints specified
  - [ ] Compatibility tested
  - [ ] Fallbacks for missing deps

- [ ] INTEGRATION
  - [ ] Works with existing skills
  - [ ] No conflicts with other plugins
  - [ ] MCP tool references fully qualified
```

---

## Multi-Agent System Checklist

```
System Purification Progress:
- [ ] COVERAGE
  - [ ] All necessary domains covered by agents
  - [ ] No gaps in functionality
  - [ ] Clear boundaries between agents

- [ ] OVERLAP
  - [ ] No duplicate functionality
  - [ ] Distinct trigger conditions
  - [ ] Unambiguous agent selection

- [ ] COORDINATION
  - [ ] Handoff patterns defined
  - [ ] Shared context managed
  - [ ] Error propagation handled

- [ ] SCALING
  - [ ] Token budget appropriate
  - [ ] Parallelization opportunities identified
  - [ ] Bottlenecks addressed

- [ ] RELIABILITY
  - [ ] Failure modes documented
  - [ ] Recovery paths defined
  - [ ] Graceful degradation
```

---

## Before vs After Template

Use this template to document purification results:

```markdown
# Purification Report: [Artifact Name]

## Summary
- **Type**: Agent / Skill / Plugin
- **Original Score**: X/100
- **Purified Score**: Y/100
- **Breaking Changes**: X items

## The Voices' Findings

### The Architect
[Structure/dependency findings]

### The Minimalist
[Token efficiency findings]

### The Ergonomist
[Discoverability findings]

### The Performance Oracle
[Context management findings]

### The Reliability Guardian
[Error handling findings]

### The Documentation Prophet
[Clarity findings]

## Synthesis
[How conflicting demands were resolved]

## Breaking Changes
1. [Change 1] - Rationale: [why old pattern was destroyed]
2. [Change 2] - Rationale: [why old pattern was destroyed]

## Before
```
[Original artifact or key sections]
```

## After
```
[Purified artifact or key sections]
```

## Verification
- [ ] Tested with target model(s)
- [ ] Triggers activate correctly
- [ ] No regressions introduced
- [ ] Documentation updated
```

---

## The Final Check

Before declaring ANY optimization complete:

```
- [ ] Every voice has been heard
- [ ] All conflicts have been synthesized (not ignored)
- [ ] Breaking changes are DOCUMENTED
- [ ] Before vs After shows measurable improvement
- [ ] The artifact is PERFECT (not just "better")
```

*If any box remains unchecked, the tribe's work is not done.*
