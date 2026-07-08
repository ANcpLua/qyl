# Batteries — the "batteries included" extended standard library for Lean 4

**One-liner:** The community-maintained standard-library extension for the Lean 4 theorem prover / dependently-typed programming language — verified data structures, meta-programming tactics, an environment linter framework, editor code-actions, and a tree-shaking import tool.

**Stack / language:** 100% **Lean 4** (`leanprover/lean4:v4.30.0`), built with the **Lake** build system. ~33,000 lines across ~180 `.lean` files. Formerly known as `std4`. Apache-2.0. Upstream: `leanprover-community/batteries`.

---

## 1. What it is

Batteries sits one layer above Lean core and one layer below Mathlib. It is the shared foundation both "computer-science" and "mathematics" users of Lean depend on. Everything ships with **machine-checked proofs of correctness** — data structures come paired with `Basic` (implementation) + `Lemmas` (proofs of invariants) files. It is simultaneously:

1. A **verified data-structure library** (union-find, heaps, red-black trees, KMP string matcher, Mersenne Twister, etc.).
2. A **meta-programming / tactic library** (custom tactics implemented in Lean's `MetaM`/`Elab` monads).
3. A **linter framework** (`#lint`) that statically audits declarations for hygiene problems.
4. **LSP code-actions** (editor quick-fixes) and a standalone **import tree-shaking CLI** (`shake`).

## 2. Architecture

The repo is a Lake package (`lakefile.toml`) exposing:

- `Batteries` lean_lib — the library itself, root `Batteries.lean` re-exports everything.
- `BatteriesTest` lean_lib — one test file per feature under `BatteriesTest/` (the `testDriver`).
- `runLinter` lean_exe — runs the environment linter over the built library (`lintDriver`).
- `shake` lean_exe (`Shake/Main.lean`) — unused-import detector operating on compiled `.olean` files.
- `test` lean_exe — test harness.

`leanOptions` turns on `linter.missingDocs = true` — every public declaration must be documented, enforced at build time. The `module` / `public import` / `@[expose] public section` syntax throughout is Lean 4's new **module system** (visibility-controlled imports).

Layered directory structure:

| Dir | Role |
|-----|------|
| `Batteries/Data/` | Verified data structures (17.9k LOC — the bulk) |
| `Batteries/Tactic/` | Custom tactics & commands (4.0k LOC) |
| `Batteries/Lean/` | Extensions/lemmas over Lean-core types (`Expr`, `HashMap`, `Json`, `Syntax`…) |
| `Batteries/Classes/` | Type-class infrastructure (`Order`, `SatisfiesM`, `RatCast`) |
| `Batteries/Control/` | Monad theory (`LawfulMonad`, `OptionT`, `AlternativeMonad`) |
| `Batteries/CodeAction/` | LSP editor quick-fix providers |
| `Batteries/Linter/` | Syntactic linters (`UnreachableTactic`, `UnnecessarySeqFocus`) |
| `Batteries/Util/` | Meta utilities (`Cache`, `Pickle`, `LibraryNote`, `ProofWanted`) |
| `Shake/` | Standalone import tree-shaker |
| `scripts/` | CI tooling: `check_imports`, `runLinter`, whitespace lint, adaptation-PR scripts |

## 3. Module map (significant files)

### Data structures (`Batteries/Data/`)
- `UnionFind/Basic.lean` (583 L) — disjoint-set with path compression + union-by-rank; nodes are `Nat < size`, `push`/`union`/`find` ops, amortized near-linear. `Lemmas.lean` proves the invariants.
- `Array/Match.lean` (171 L) — Knuth-Morris-Pratt **prefix table** with a `valid` invariant baked into the type; generic `Matcher` over any `BEq`/`Stream`.
- `String/Matcher.lean` (128 L) — KMP specialized to `Char`/`String`; `ofString`, `find?`, `findAll`.
- `Random/MersenneTwister.lean` (160 L) — parameterized MT PRNG; `Config` struct covers MT19937 (32-bit) & MT19937-64; exposes both `RandomGen` and `Stream` interfaces.
- `RunningStats.lean` (70 L) — Welford's one-pass online mean/variance/stddev.
- `BinomialHeap/`, `PairingHeap.lean`, `BinaryHeap.lean` — priority queues (Basic+Lemmas each).
- `RBMap/` — red-black tree map/set with balancing proofs (`del`, insert, `Ordering`-cut based).
- `MLList/Basic.lean` — monadic lazy lists ("laziness" driven by an arbitrary monad).
- `DList.lean` (difference lists), `AssocList.lean`, `ByteSlice.lean`/`ByteArray.lean`, `Vector.lean` (length-indexed), `FloatArray.lean`, `UnionFind`, `Nat`/`Int`/`Rat`/`Fin`/`BitVec`/`UInt`/`Char`/`Bool`/`Range`/`Stream`/`NameSet` extensions.

### Tactics & commands (`Batteries/Tactic/`)
- `Lint/` — the **environment linter** framework: `Basic` (the `Linter`/`@[env_linter]` abstraction), `Frontend` (`#lint`, `#lint in Pkg`, `#list_linters` commands, `*`/`-`/`+` modifiers), `Simp`, `TypeClass`, `Misc` checks.
- `Alias.lean` — `alias` command to create declaration synonyms.
- `Congr.lean` (`congr with`, `rcongr`), `Trans.lean` (extensible transitivity), `Exact.lean` (`MetaM` exact), `Case.lean`, `PermuteGoals.lean` (`on_goal`, `pick_goal`), `SeqFocus.lean` (`<;>` mapping).
- `OpenPrivate.lean` — access private declarations by collecting private names referenced in a def.
- `HelpCmd.lean` (`#help`), `Instances.lean` (`#instances`), `PrintPrefix`/`PrintOpaques`/`PrintDependents`, `ShowUnused.lean`, `GeneralizeProofs.lean`, `SqueezeScope.lean`, `Lemma.lean`, `NoMatch.lean`, `Unreachable.lean`.

### Meta utilities (`Batteries/Util/`, `Batteries/Lean/`)
- `Util/Cache.lean` — once-per-file lazy `Cache α` and environment-fold `DeclCache α` (see excerpt).
- `Util/Pickle.lean` — serialize/deserialize objects to disk (compiled-oleans style).
- `Util/LibraryNote.lean` — `library_note` prose-annotation system; `Util/ProofWanted.lean` — declares an unproved target.
- `Lean/*` — lawful-monad instances, `Expr`/`Syntax` helpers, `HashMap`/`PersistentHashMap`/`HashSet` extensions, `Json`, `SatisfiesM`, `MonadBacktrack`, `EStateM`, `TagAttribute`, `NameMapAttribute`.

### Code actions & linters
- `CodeAction/Misc.lean` — tactic quick-fixes via `@[tactic_code_action]`.
- `CodeAction/Deprecated.lean` — auto-replace deprecated names; `Match.lean` — fill match arms; `Attr.lean`/`Basic.lean` framework.
- `Linter/UnreachableTactic.lean`, `Linter/UnnecessarySeqFocus.lean` — syntactic (post-elaboration) linters.

### Tooling
- `Shake/Main.lean` (651 L) — CLI import tree-shaker with `--fix`, `--update`, `--no-downstream`; config in `scripts/noshake.json`.
- `scripts/check_imports.lean`, `scripts/runLinter.lean`, `scripts/lintWhitespace.sh`.

## 4. Notable code

### (a) Invariant-carrying KMP prefix table — `Batteries/Data/Array/Match.lean:12`
The correctness invariant is a *field of the structure*, so any value is correct by construction; the transition function's termination is proved inline.
```lean
structure PrefixTable (α : Type _) extends Array (α × Nat) where
  /-- Validity condition to help with termination proofs -/
  valid : (h : i < toArray.size) → toArray[i].2 ≤ i

def PrefixTable.step [BEq α] (t : PrefixTable α) (x : α) : Fin (t.size+1) → Fin (t.size+1)
  | ⟨k, hk⟩ =>
    let cont := fun () =>
      match k with
      | 0 => ⟨0, Nat.zero_lt_succ _⟩
      | k + 1 =>
        let k' := t.toArray[k].2
        have hk' : k' < k + 1 := Nat.lt_succ_of_le (t.valid h2)
        step t x ⟨k', Nat.lt_trans hk' hk⟩
    if hsz : k < t.size then
      if x == t.toArray[k].1 then ⟨k+1, Nat.succ_lt_succ hsz⟩ else cont ()
    else cont ()
termination_by k => k.val
```
The `Fin (t.size+1)` return type *guarantees* the match position is in range — no out-of-bounds possible.

### (b) `parentD` / `rankD` total accessors — `Batteries/Data/UnionFind/Basic.lean:24`
Union-find avoids partiality by making array reads total: an out-of-range index is its own parent (a root), rank 0. This one trick removes a whole class of bounds obligations from every downstream proof.
```lean
def parentD (arr : Array UFNode) (i : Nat) : Nat :=
  if h : i < arr.size then arr[i].parent else i
def rankD (arr : Array UFNode) (i : Nat) : Nat :=
  if h : i < arr.size then arr[i].rank else 0
theorem lt_of_parentD : parentD arr i ≠ i → i < arr.size :=
  Decidable.not_imp_comm.1 parentD_of_not_lt
```

### (c) Once-per-file tactic cache — `Batteries/Util/Cache.lean:41`
Exploits "one process per Lean file" to memoize expensive environment scans (e.g. building discrimination trees over all imports) across tactic invocations. `Cache α` stores either the uncomputed thunk or an async `Task`.
```lean
def Cache (α : Type) := IO.Ref <| MetaM α ⊕ Task (Except Exception α)
instance : Nonempty (Cache α) := inferInstanceAs <| Nonempty (IO.Ref _)
```
`DeclCache` layers on top: the fold over *imported* constants is cached, while constants in the current file are re-folded each call — the exact right cache boundary for incremental editing.

### (d) Welford online statistics — `Batteries/Data/RunningStats.lean`
One-pass mean/variance keeping only running `M` and `S`, so unbounded streams need O(1) memory:
```
Mₖ = Mₖ₋₁ + (xₖ - Mₖ₋₁)/k
Sₖ = Sₖ₋₁ + (xₖ - Mₖ₋₁)*(xₖ - Mₖ)
μₖ = Mₖ ,  σ²ₖ = Sₖ/k ,  s²ₖ = Sₖ/(k-1)
```

### (e) Shake — olean-based unused-import detection — `Shake/Main.lean:9`
Rather than re-elaborating, Shake reads compiled `.olean` files, deduces which constants each import contributes, and flags imports contributing nothing. Checks all of Mathlib in ~8s. Known-false-positive suppression lives in `scripts/noshake.json`; `--update` auto-populates it. Honest about its blind spots (tactics/notation leave no proof-term trace) in the header doc.

## 5. Extractable value (reusable elsewhere)

- **Invariant-as-a-field pattern** (`PrefixTable.valid`, union-find `WellFormed`): encode data-structure invariants as struct fields / refinement types so instances are correct-by-construction. Portable to any language with dependent/refinement types (F*, Idris, Rust type-state).
- **Total-accessor trick** (`parentD`/`rankD`): make array/graph reads total by mapping out-of-range to a semantic identity (self-parent, zero rank). Eliminates bounds-checking noise in proofs *and* in ordinary code.
- **KMP prefix-table matcher** generic over `BEq`/`Stream` — directly reusable substring search that separates pattern-compilation from scanning (reuse compiled matcher across many inputs).
- **Mersenne Twister with a `Config` struct** parameterizing all MT variants (word size, twist, tempering masks) — a clean template for parameterized PRNG families exposing both pull (`RandomGen`) and push (`Stream`) interfaces.
- **Welford running-stats** — copy-paste-able online mean/variance for any telemetry/metrics pipeline (relevant to qyl-style observability: O(1) streaming aggregation with no sample retention — mirrors the interval-accounting pattern already used in `QylAgentInventory`).
- **Once-per-process lazy cache boundary** (`Cache`/`DeclCache`): the "cache the immutable prefix, recompute the mutable tail" idea generalizes to any incremental analyzer / language server / build tool.
- **olean/artifact-based tree-shaking** (`shake`): analyze *compiled outputs* instead of re-parsing source to find dead dependencies — fast, and a model for any dependency-pruning tool that has access to build artifacts. The JSON false-positive-suppression + `--update` self-learning loop is a nice ergonomic pattern.
- **Environment-linter framework** (`@[env_linter]` + `#lint` frontend with `*`/`-`/`+`/`only` modifiers): pluggable static-audit architecture where checks register via attribute and the frontend discovers them — transplantable to any extensible-linting need.
- **`library_note` / `proof_wanted`**: lightweight in-source prose-annotation and "TODO with a type signature" primitives for large collaborative codebases.

## 6. Build / run

```sh
# Toolchain (elan manages the pinned lean-toolchain = v4.30.0):
elan self update            # or install elan via elan-init.sh
lake build                  # build the library
lake test                   # build + run all BatteriesTest/*
lake lint                   # run the environment linter (runLinter exe)
lake exe shake Batteries    # detect unused imports
scripts/updateBatteries.sh  # regenerate import list after adding a file

# Docs:
cd docs && lake build Batteries:docs   # HTML at docs/doc/index.html
```
Consume as a dependency via `lakefile.toml`:
```toml
[[require]]
name = "batteries"
scope = "leanprover-community"
rev = "main"
```
CI (`.github/workflows/build.yml`) builds + tests + lints; nightly workflows auto-bump against Lean nightlies and test against Mathlib (`test_mathlib.yml`). `bors.toml` gates merges.
