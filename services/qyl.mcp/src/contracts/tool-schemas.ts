/**
 * The generated JSON Schema documents, registered as they are.
 *
 * contracts/scripts/emit-mcp-schemas.mjs writes one document per tool input and output model and
 * per Collector body the server reads, already inlined and sealed for every host. Each becomes a
 * Standard Schema through the SDK's `fromJsonSchema`, compiled once by one strict Ajv with its
 * formats: the same validator checks a tool's arguments and the Collector body its result is
 * built from.
 *
 * The validator is this module's `jsonSchemaValidator` (schema-libraries.md, "Swap the JSON
 * Schema validator"): a rejection names every offending argument by its path and, where the
 * schema enumerates them, the values or names it accepts, so the SDK's input-validation error
 * (tools.md, "Send arguments the schema rejects") tells the model what to change. The tool-call
 * frame reads the same issues back through `argumentIssues` for the structured error.
 */
import {
  fromJsonSchema,
  type JsonSchemaType,
  type JsonSchemaValidator,
  type jsonSchemaValidator,
  type StandardSchemaWithJSON,
} from "@modelcontextprotocol/server";
import { addFormats, Ajv } from "@modelcontextprotocol/server/validators/ajv";
import type { ToolError } from "./generated/api.js";
import type { ContractDefinitions } from "./generated/definitions.js";
import documents from "./generated/tool-schemas.json" with { type: "json" };

// verbose: an additionalProperties error carries its object schema, whose property names are
// what the rejection lists as accepted.
const ajv = new Ajv({ strict: true, allErrors: true, verbose: true });
addFormats(ajv);

/** A value the contract's `ToolError.allowed` names: one a schema enumerates for an argument. */
export type ArgumentValue = NonNullable<ToolError["allowed"]>[number];

/** One rule a value broke, named by its path. Never carries the rejected value itself. */
export interface ArgumentIssue {
  /** Path as the Collector spells it: `status`, `filter.signal`, `matchers[0].op`; empty for the value itself. */
  readonly field: string;
  readonly message: string;
  /** The JSON Schema keyword that failed: `enum`, `required`, `additionalProperties`, … */
  readonly keyword: string;
  /** The values, or for an unknown name the names, the schema accepts at `field`. */
  readonly allowed?: readonly ArgumentValue[];
}

interface AjvError {
  readonly keyword: string;
  readonly instancePath: string;
  readonly schemaPath: string;
  readonly params: Record<string, unknown>;
  readonly message?: string;
  readonly parentSchema?: unknown;
}

const rejected = new WeakMap<object, readonly ArgumentIssue[]>();

/** The issues of the last failed validation of exactly this object, such as a call's arguments. */
export function argumentIssues(value: unknown): readonly ArgumentIssue[] | undefined {
  return typeof value === "object" && value !== null ? rejected.get(value) : undefined;
}

export function formatArgumentIssue(issue: ArgumentIssue): string {
  return `${issue.field || "arguments"}: ${issue.message}`;
}

function list(values: readonly unknown[]): string {
  return values.map((value) => (typeof value === "string" ? value : JSON.stringify(value))).join(", ");
}

/** The enumerated values a model can send back; a schema's null or object members are not among them. */
function scalars(values: readonly unknown[]): ArgumentValue[] {
  return values.filter((value): value is ArgumentValue =>
    typeof value === "string" || typeof value === "number" || typeof value === "boolean");
}

/** A JSON Pointer as the Collector names a field: properties joined by dots, array indexes in brackets. */
function path(instancePath: string, child?: unknown): string {
  const segments = instancePath.split("/").slice(1).map((segment) => segment.replaceAll("~1", "/").replaceAll("~0", "~"));
  if (typeof child === "string") segments.push(child);
  return segments.reduce((joined, segment) =>
    /^\d+$/u.test(segment) ? `${joined}[${segment}]` : joined === "" ? segment : `${joined}.${segment}`, "");
}

function describe(error: AjvError): ArgumentIssue {
  switch (error.keyword) {
    case "required":
      return { field: path(error.instancePath, error.params.missingProperty), keyword: "required", message: "is required" };
    case "additionalProperties": {
      const parent = error.parentSchema as { properties?: Record<string, unknown> } | undefined;
      const accepted = Object.keys(parent?.properties ?? {});
      return {
        field: path(error.instancePath, error.params.additionalProperty),
        keyword: "additionalProperties",
        message: accepted.length === 0 ? "is not accepted here" : `is not accepted here; accepted: ${list(accepted)}`,
        ...(accepted.length === 0 ? {} : { allowed: accepted }),
      };
    }
    case "enum":
    case "const": {
      const values = error.keyword === "enum" ? error.params.allowedValues as unknown[] : [error.params.allowedValue];
      return { field: path(error.instancePath), keyword: "enum", message: `must be one of: ${list(values)}`, allowed: scalars(values) };
    }
    default:
      return { field: path(error.instancePath), keyword: error.keyword, message: error.message ?? `fails ${error.keyword}` };
  }
}

const issueKey = (issue: Pick<ArgumentIssue, "field" | "keyword">): string => `${issue.field}\u0000${issue.keyword}`;

/**
 * Keep the branch errors of every `oneOf`/`anyOf` that explain the rejection. TypeSpec unions are
 * discriminated, so the useful answer is one of three: the rule every branch breaks the same way
 * (a missing or unknown discriminator, a value of none of the allowed types), else the errors of
 * the one branch whose discriminator matched, else the union's own error.
 */
function resolveUnions(errors: readonly AjvError[]): AjvError[] {
  let remaining = [...errors];
  const unions = errors
    .filter((error) => error.keyword === "oneOf" || error.keyword === "anyOf")
    .sort((left, right) => left.schemaPath.length - right.schemaPath.length);
  for (const union of unions) {
    if (!remaining.includes(union)) continue;
    const prefix = `${union.schemaPath}/`;
    const branches = new Map<string, AjvError[]>();
    for (const error of remaining) {
      if (!error.schemaPath.startsWith(prefix)) continue;
      const branch = error.schemaPath.slice(prefix.length).split("/", 1)[0]!;
      branches.set(branch, [...(branches.get(branch) ?? []), error]);
    }
    if (branches.size === 0) continue;
    const perBranch = [...branches.values()];
    const shared = perBranch.length < 2 ? [] : perBranch[0]!.filter((error) => {
      const key = issueKey(describe(error));
      return perBranch.every((errorsOfBranch) => errorsOfBranch.some((other) => issueKey(describe(other)) === key));
    });
    const discriminatorMiss = (errorsOfBranch: readonly AjvError[]): boolean => errorsOfBranch.some((error) =>
      (error.keyword === "const" || (error.keyword === "enum" && (error.params.allowedValues as unknown[]).length === 1))
      && error.instancePath.startsWith(`${union.instancePath}/`)
      && !error.instancePath.slice(union.instancePath.length + 1).includes("/"));
    const matching = perBranch.filter((errorsOfBranch) => !discriminatorMiss(errorsOfBranch));
    const keep = shared.length > 0
      ? perBranch.flat().filter((error) => shared.some((sharedError) => issueKey(describe(sharedError)) === issueKey(describe(error))))
      : matching.length === 1 ? matching[0]! : [union];
    remaining = remaining.filter((error) => error !== union && !error.schemaPath.startsWith(prefix)).concat(keep);
  }
  return remaining;
}

const PRIORITY = ["additionalProperties", "enum", "required", "type"];
const priority = (issue: ArgumentIssue): number => {
  const rank = PRIORITY.indexOf(issue.keyword);
  return rank < 0 ? PRIORITY.length : rank;
};

/** One issue per path and rule; enum values and alternative types from several branches merge. */
function describeAjvErrors(errors: readonly AjvError[]): ArgumentIssue[] {
  const issues = new Map<string, ArgumentIssue>();
  for (const error of resolveUnions(errors)) {
    const issue = describe(error);
    const key = issueKey(issue);
    const existing = issues.get(key);
    if (existing === undefined) {
      issues.set(key, issue);
    } else if (issue.keyword === "enum") {
      const allowed = [...new Set([...(existing.allowed ?? []), ...(issue.allowed ?? [])])];
      issues.set(key, { ...existing, allowed, message: `must be one of: ${list(allowed)}` });
    } else if (issue.keyword === "type" && typeof error.params.type === "string") {
      const types = [...new Set([...existing.message.replace(/^must be /u, "").split(" or "), error.params.type])];
      issues.set(key, { ...existing, message: `must be ${types.join(" or ")}` });
    }
  }
  return [...issues.values()].sort((left, right) => priority(left) - priority(right));
}

/** The one JSON Schema validator every generated document is compiled with. */
class ContractValidator implements jsonSchemaValidator {
  getValidator<T>(schema: JsonSchemaType): JsonSchemaValidator<T> {
    const validate = ajv.compile(schema);
    return (input) => {
      if (validate(input)) return { valid: true, data: input as T, errorMessage: undefined };
      const issues = describeAjvErrors((validate.errors ?? []) as readonly AjvError[]);
      if (typeof input === "object" && input !== null) rejected.set(input, issues);
      return { valid: false, data: undefined, errorMessage: issues.map(formatArgumentIssue).join("; ") };
    };
  }
}

export const contractValidator = new ContractValidator();

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
