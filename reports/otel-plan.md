# Repair Plan: examples/otel

## Status
REPAIRABLE

## Source Safety
The source package will not be modified.

## Problems Found

### 1. Invalid animation row
- file: pet.json
- field: animations.review.row
- current: 9
- expected: 0..8
- proposed: 3
- owner: pet.json animations

### 2. Frankenstein ability metadata unsupported by Codex target
- file: pet.json
- field: abilities
- current: root field
- expected: x-frankenstein.abilities
- proposed: x-frankenstein.abilities
- owner: Codex exporter

## Proposed Output

```txt
.tmp/frankenstein/otel-repaired/
  pet.json
  spritesheet.webp
  frankenstein-repair-manifest.json
```

## Verification After Repair

```bash
frankenstein validate .tmp/frankenstein/otel-repaired --target codex
frankenstein import .tmp/frankenstein/otel-repaired --out .tmp/frankenstein/imported.json
frankenstein diff-normalized examples/otel .tmp/frankenstein/otel-repaired
```

## Requires Approval

YES
