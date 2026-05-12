# core/specs/generated

This directory previously held the Weaver-generated `otel-keys.gen.tsp` file.

As of the swap to `@o-ancpplua/otel-conventions-api`, those TypeSpec consts ship via
the npm package and are no longer generated locally. Consumers import them through
the subpath:

```tsp
import "@o-ancpplua/otel-conventions-api/generated/otel-keys";

// ANcpLua.OtelConventions.OTel.Keys.<Domain>.<Ident>
@encodedName("application/json", ANcpLua.OtelConventions.OTel.Keys.GenAi.System)
system?: string;
```

The producer (upstream OTel semantic-conventions YAML to TypeSpec) lives in the
repository `ANcpLua/typespec-otel-semconv`. It pins the upstream
`open-telemetry/semantic-conventions` release (currently v1.41.0) and emits the
file consumed here.

`./eng/semconv/run-weaver.sh` still generates the qyl-side outputs (TS semconv,
DuckDB promoted-columns, attribute registry, qyl-attribute C# constants, qyl docs)
but no longer writes anything to this directory.
