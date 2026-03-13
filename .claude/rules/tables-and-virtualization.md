# tables and virtualization

## Role in qyl
Use TanStack Table for dense operational surfaces such as:
- logs
- traces
- spans
- incidents
- alerts
- releases
- entities
- issue result lists

Use TanStack Virtual when rendering scale requires virtualization.

## Important positioning
TanStack Table is a headless table engine, not a single batteries-included grid widget.
TanStack Virtual is the scalable rendering companion, not a style system.

## Required capabilities for qyl tables
Large product tables should support the relevant subset of:
- sorting
- filtering
- pagination
- visibility toggles
- row selection
- column composition
- sticky headers where useful
- keyboard-friendly navigation
- virtualization for large datasets

## Design rules
Prefer:
- scanable rows
- restrained cell chrome
- strong typography hierarchy
- compact spacing where operationally justified
- sticky controls for real workflows

Avoid:
- giant cardified rows for log-like data
- gratuitous whitespace
- table abstractions that hide TanStack strengths
- premature virtualization for tiny datasets

## Performance rule
If the screen clearly targets very large result sets, make virtualization a deliberate design decision instead of an afterthought.
