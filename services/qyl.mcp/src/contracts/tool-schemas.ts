/**
 * The generated JSON Schema documents, registered as they are.
 *
 * contracts/scripts/emit-mcp-schemas.mjs writes one document per tool input and output model and
 * per Collector body the server reads, already inlined and sealed for every host. Each becomes a
 * Standard Schema through the SDK's `fromJsonSchema`, compiled once by one strict Ajv with its
 * formats: the same validator checks a tool's arguments, its structured result and the Collector
 * body it was built from.
 */
import { fromJsonSchema, type JsonSchemaType, type StandardSchemaWithJSON } from "@modelcontextprotocol/server";
import { addFormats, Ajv, AjvJsonSchemaValidator } from "@modelcontextprotocol/server/validators/ajv";
import type { ContractDefinitions } from "./generated/definitions.js";
import documents from "./generated/tool-schemas.json" with { type: "json" };

const ajv = new Ajv({ strict: true, allErrors: true });
addFormats(ajv);

/** The one JSON Schema validator every generated document is compiled with. */
export const contractValidator = new AjvJsonSchemaValidator(ajv);

/** A generated document's name: its OpenAPI component name, e.g. `Mcp.Tools.TriageInput`. */
export type ContractSchemaName = keyof typeof documents & keyof ContractDefinitions;

export type ContractSchema<Name extends ContractSchemaName> =
  StandardSchemaWithJSON<ContractDefinitions[Name], ContractDefinitions[Name]>;

const compiled = new Map<string, StandardSchemaWithJSON<unknown, unknown>>();

/**
 * The generated document `name` as a Standard Schema. createServer() runs per hosted request, so
 * each document is compiled on first use and shared by every server instance after that.
 */
export function contractSchema<Name extends ContractSchemaName>(name: Name): ContractSchema<Name> {
  let schema = compiled.get(name);
  if (schema === undefined) {
    // `in` would also find Object.prototype keys; only the generated names are documents.
    if (!Object.hasOwn(documents, name)) throw new Error(`The generated contract has no '${name}' document.`);
    schema = fromJsonSchema<ContractDefinitions[Name]>(documents[name] as JsonSchemaType, contractValidator);
    compiled.set(name, schema);
  }
  return schema as ContractSchema<Name>;
}

export type ContractCheck<T> = { valid: true; value: T } | { valid: false; message: string };

/** Validate `value` against the generated document `name`. */
export function checkContract<Name extends ContractSchemaName>(name: Name, value: unknown): ContractCheck<ContractDefinitions[Name]> {
  const result = contractSchema(name)["~standard"].validate(value);
  // fromJsonSchema validates synchronously; a promise would mean another validator was wired in.
  if (result instanceof Promise) throw new Error(`The '${name}' validator is asynchronous.`);
  return result.issues === undefined
    ? { valid: true, value: result.value }
    : { valid: false, message: result.issues.map((issue) => issue.message).join("; ") };
}
