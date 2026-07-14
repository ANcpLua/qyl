# Qyl.DuckDb.Aot

Native-AOT bridge for [DuckDB.NET](https://github.com/Giorgi/DuckDB.NET) (verified against
1.5.3). Lives in the qyl repo today; deliberately has **zero qyl dependencies** so it can be
extracted into a standalone package once it has proven itself. Upstream annotation request:
[Giorgi/DuckDB.NET#339](https://github.com/Giorgi/DuckDB.NET/issues/339) — when upstream
annotates, this shim shrinks to nothing and gets deleted.

## The problem

`DuckDB.NET.Data` ships without `IsTrimmable`/`IsAotCompatible` and has ten trim/AOT warning
sites (enumerated in #339). Two of them are load-bearing at runtime under Native AOT:

| Provider path | Mechanism | AOT failure mode |
|---|---|---|
| `Nullable<U>` reads (`GetFieldValue<U?>`, nullable scalar/table-function params) | `NullableHandler<T>.Compile()` → `MakeGenericMethod(GetValidValue<U>)` over an internal generic **virtual** method | `MissingMetadataException`-class failure on first nullable read of a `U` whose instantiation is not in the image |
| LIST/MAP materialization via non-generic `GetValue`/`GetValues`/`GetSchemaTable` | `Type.MakeGenericType(List<>, elem)` + `Activator.CreateInstance` | runtime failure for element types whose closed `List<T>` was never constructed statically |

Additionally, STRUCT→POCO materialization (`Activator.CreateInstance` + `GetProperties()`)
degrades **silently** under trimming: stripped setters simply leave properties default.

## Why not `[DynamicDependency]` / rd.xml / `TrimmerRootAssembly`

They root **metadata**, not value-type generic **instantiations**. Under ILC,
`MakeGenericMethod(typeof(int))` over a value type succeeds only if `GetValidValue<int>` was
compiled into the image — which requires a *statically reachable* call site. Rooting the
assembly keeps the generic definition and still fails at runtime.

## The mechanism

`DuckDbAot.Warmup()` executes real queries against `DataSource=:memory:` and reads:

1. **Every DuckDB scalar type** through `GetFieldValue<U>` — each call site is a static
   generic instantiation; because `GetValidValue<U>` is a generic *virtual* method, ILC's
   whole-program GVM analysis expands it across **all** vector readers (Numeric, Decimal,
   DateTime, Guid, Boolean, String, Interval, …), which is exactly the lookup set
   `MakeGenericMethod` probes at runtime.
2. **The same set as `Nullable<U>`** over a NULL row and a value row — this drives
   `NullableHandler<U?>.Compile()` for real, proving the step-1 rooting worked.
3. **LIST columns** through the non-generic `GetValue` path, after statically constructing
   `new List<T>()` for the full element-type set (satisfies both `MakeGenericType` and the
   `Activator` parameterless-ctor requirement).
4. **MAP and STRUCT columns**: MAP materializes `Dictionary<K,V>` from the *concrete*
   key/value CLR types (`MapVectorDataReader.GetColumnType` calls
   `MakeGenericType(keyReader.ClrType, valueReader.ClrType)`), so the VARCHAR-keyed family
   over the primitive value set is pre-rooted and warmup reads a value-typed and a
   string-valued MAP for real. STRUCT columns read through the boxed path materialize
   `Dictionary<string, object>` — also rooted and warmup-verified.

Because warmup runs the provider's actual read paths, a missing instantiation is a **loud
startup failure** with the type named — never a corrupted query result in production.

For STRUCT→POCO reads, call `DuckDbAot.RootStruct<TPoco>()` once per POCO type; its
`[DynamicallyAccessedMembers]` annotation makes the trimmer keep the constructor and property
setters the provider populates reflectively.

## Consuming from an AOT-published application

1. Reference this project (later: package).
2. Call `DuckDbAot.Warmup()` once at startup, before the first DuckDB query. Cost on
   JIT runtimes: one in-memory DuckDB connection, ~milliseconds — it doubles as a provider
   behavior self-test. If you want it AOT-only:
   `if (!RuntimeFeature.IsDynamicCodeSupported) DuckDbAot.Warmup();`
3. Import `build/Qyl.DuckDb.Aot.props` (packaged builds get it transitively) — it
   `NoWarn`s the two assembly-level rollups (`IL2104`, `IL3053`) that the unannotated
   DuckDB.NET.Data assembly triggers, *only* when `PublishAot=true`.
4. Reads of nullable columns through the boxed `GetValue(ordinal)` + `Convert.*` pattern
   (ADO.NET style) never touch the nullable-handler path at all — that pattern is
   AOT-safe by construction and preferable on hot paths (the compiled nullable delegates
   run on the LINQ expression *interpreter* under AOT).

## Known limits

- `Expression.Compile` falls back to the interpreter under AOT: nullable typed reads are
  correct but slower than `IsDBNull` + non-nullable getters. Prefer the latter on hot paths.
- **Consumer-defined enums cannot be pre-rooted by construction.** Reading a column as
  `GetFieldValue<MyEnum?>` without any statically reachable non-nullable `MyEnum` accessor
  fails at query time under AOT. Call `DuckDbAot.RootFieldType<MyEnum>()` once (the
  NON-nullable type — that is the instantiation `NullableHandler<MyEnum?>` probes for).
- **MAP key/value combinations are combinatorial.** VARCHAR-keyed maps over primitive values
  are pre-rooted; any other pair needs `DuckDbAot.RootMap<K, V>()` in the consumer.
- Provider-specific structs (`DuckDBHugeInt`, `DuckDBUHugeInt`, `DuckDBInterval`,
  `DuckDBDateOnly`, `DuckDBTimeOnly`, `DuckDBTimestamp`) are rooted via `RootFieldType<T>`
  (mechanism-proven) but not query-verified by warmup.
- The element-type closed set for LIST covers DuckDB's primitive types. Exotic element
  types (nested lists, provider-specific `DuckDB*` structs) need a static
  `new List<YourType>()` in the consumer.
- `GetSchemaTable`/`DataTable.Load` over LIST/MAP columns additionally hits the `IL2093`
  annotation mismatch on `GetFieldType` — fixable only upstream.
- Appender (`DuckDBAppender`) paths are not exercised by warmup (qyl does not use them);
  they are P/Invoke-shaped and expected AOT-safe, but unverified here.

## Maintenance contract

On every DuckDB.NET version bump: publish any AOT consumer with
`-p:TrimmerSingleWarn=false`, diff the per-site IL warnings against the table in #339, and
extend `Warmup()` if the provider grew new reflective paths. The warmup failing at startup
is the signal that the closed set drifted.
