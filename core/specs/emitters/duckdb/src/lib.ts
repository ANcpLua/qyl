import { createTypeSpecLibrary, paramMessage } from "@typespec/compiler";

export const $lib = createTypeSpecLibrary({
  name: "@qyl/typespec-emit-duckdb",
  diagnostics: {
    "unmapped-type": {
      severity: "error",
      messages: {
        default: paramMessage`QYL-DUCK-001: unmapped DuckDB column type '${"name"}' — add a mapping or @duckdbColumn override`,
      },
    },
    "missing-primary-key": {
      severity: "warning",
      messages: {
        default: paramMessage`QYL-DUCK-002: table '${"name"}' has no @duckdbPrimaryKey column`,
      },
    },
  },
  state: {
    duckdbTable: { description: "DuckDB table name on a model" },
    duckdbColumn: { description: "DuckDB column name override on a property" },
    duckdbType: { description: "DuckDB column type override on a property or scalar" },
    duckdbPrimaryKey: { description: "Marks property as DuckDB PRIMARY KEY" },
    duckdbIndex: { description: "DuckDB index declaration on a property" },
    duckdbRowTimestamp: { description: "Opt-in auto-created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP column" },
  },
} as const);

export const { reportDiagnostic, stateKeys } = $lib;
