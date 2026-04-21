import type { Program, Scalar, Type } from "@typespec/compiler";
import { isArrayModelType, isRecordModelType, getFormat } from "@typespec/compiler";
import { reportDiagnostic } from "./lib.js";

const SCALAR_MAP: Record<string, string> = {
  "int8": "TINYINT",
  "int16": "SMALLINT",
  "int32": "INTEGER",
  "int64": "BIGINT",
  "uint8": "UTINYINT",
  "uint16": "USMALLINT",
  "uint32": "UINTEGER",
  "uint64": "UBIGINT",
  "float32": "REAL",
  "float64": "DOUBLE",
  "decimal": "DECIMAL(38,10)",
  "decimal128": "DECIMAL(38,10)",
  "boolean": "BOOLEAN",
  "bytes": "BLOB",
  "utcDateTime": "TIMESTAMP",
  "offsetDateTime": "TIMESTAMPTZ",
  "plainDate": "DATE",
  "plainTime": "TIME",
  "duration": "INTERVAL",
  "url": "VARCHAR",
};

export function mapType(program: Program, type: Type): string {
  switch (type.kind) {
    case "Scalar":
      return mapScalar(program, type as Scalar);
    case "Model":
      if (isArrayModelType(type)) {
        const inner = mapType(program, type.indexer!.value);
        return `${inner}[]`;
      }
      if (isRecordModelType(type)) {
        const inner = mapType(program, type.indexer!.value);
        return `MAP(VARCHAR, ${inner})`;
      }
      // Nested models become JSON blobs
      return "JSON";
    case "Enum":
      return "VARCHAR";
    case "Union":
      return "VARCHAR";
    case "Boolean":
      return "BOOLEAN";
    case "String":
      return "VARCHAR";
    case "Number":
      return "DOUBLE";
    default:
      reportDiagnostic(program, { code: "unmapped-type", target: type, format: { name: type.kind } });
      return "VARCHAR";
  }
}

function mapScalar(program: Program, scalar: Scalar): string {
  if (scalar.name === "string") {
    const format = getFormat(program, scalar as unknown as Parameters<typeof getFormat>[1]);
    if (format === "uuid") return "UUID";
    return "VARCHAR";
  }
  const direct = SCALAR_MAP[scalar.name];
  if (direct) return direct;
  if (scalar.baseScalar) return mapScalar(program, scalar.baseScalar);
  reportDiagnostic(program, { code: "unmapped-type", target: scalar, format: { name: `scalar:${scalar.name}` } });
  return "VARCHAR";
}
