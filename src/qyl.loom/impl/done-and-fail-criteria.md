# done and fail criteria

## Definition of done

A qyl frontend task is done only if:

- the chosen primitive-family contract is followed consistently
- Base UI patterns are used correctly for the Base UI variant
- no forbidden Radix imports or Radix composition patterns exist
- styling remains qyl-owned
- accessibility semantics are preserved
- keyboard flow works for the affected interaction
- the screen fits telemetry-product needs instead of generic CRUD defaults
- components are composable at the product level
- performance is acceptable for realistic dataset sizes
- lint and CI checks pass
- a future agent can understand the implementation without hidden conventions

## Auto-fail criteria

Fail the task immediately if any of the following is true:

- any import from `@radix-ui/*`
- any import from `radix-ui`
- any use of `asChild`
- any use of `Slot`
- Base UI behavior implemented through copied Radix semantics
- detached trigger behavior implemented without `createHandle()`
- heavy observability charts implemented with weaker defaults without justification
- flashy motion harms readability or operator speed
- AI-generated analysis is visually indistinguishable from raw telemetry facts
- a second competing primitive layer is introduced

## Review summary checklist

Before approving, verify:

- one primitive family only
- product-level ownership preserved
- operator-grade density maintained
- correct table and chart decisions made
- provenance and accessibility remain clear
