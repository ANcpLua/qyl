// =============================================================================
// Qyl namespace anchor — establishes "project" location context for the root
// Qyl namespace BEFORE any `@qyl/typespec-emit-*` package import declares its
// own `Qyl.Emit.*` sub-namespaces from `node_modules`.
//
// Why this exists:
// TypeSpec assigns a LocationContext to each namespace based on where it is
// FIRST declared. `@typespec/http-client-js`'s client discovery only walks
// top-level namespaces that pass `isUserDefined` (i.e. LocationContext.type ===
// "project"). Before the `@o-ancpplua/otel-conventions-api` swap, qyl's own
// `core/specs/{common,otel}/*.tsp` files declared `Qyl.Common.*`, `Qyl.OTel.*`,
// and `Qyl.Storage` — so `Qyl`'s location was anchored to the project and
// `Qyl.Api` (with 16 interfaces) was reachable through the discovery walk.
//
// After the swap those files moved to the npm package's
// `ANcpLua.OtelConventions.*` tree, leaving only `Qyl.Emit.*` (from the
// node_modules emitter packages) as the first thing TypeSpec sees under `Qyl`.
// That flipped `Qyl.isUserDefined` to `false`, and the http-client-js
// discovery short-circuited with `cant-find-client` even though `Qyl.Api`
// itself is still user-defined.
//
// This empty anchor declares `namespace Qyl;` from a project file at the
// top of main.tsp's import graph, so `Qyl` is registered with
// LocationContext.type === "project" from the start. The other Qyl.* sub
// declarations stack on top of it without changing the root's context.
//
// Remove only if a future TypeSpec / http-client-js release fixes the
// discovery walk to descend into non-user-defined parent namespaces when
// their children are user-defined.
// =============================================================================

namespace Qyl;
