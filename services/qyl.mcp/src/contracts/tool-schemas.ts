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
 *
 * A tool's arguments are read as the model meant them where that is unambiguous: an enumerated
 * string sent with its JSON quotes inside the value (`"\"rest\""`) is that string. A free-text
 * value keeps its quotes, and the Collector bodies the same validator checks are taken as sent.
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
  /** The JSON Schema keyword that failed: `enum`, `required`, `additionalProperties`, `anyOf`, … */
  readonly keyword: string;
  /**
   * The values, or for an unknown name the names, the schema accepts at `field`. A union's
   * object form is written with its members, `{matchers, window, failure_id}`.
   */
  readonly allowed?: readonly ArgumentValue[];
  /** A valid value of `field` as JSON, such as `"rest"`, when the rule is a union's. */
  readonly example?: string;
}

interface AjvError {
  readonly keyword: string;
  readonly instancePath: string;
  readonly schemaPath: string;
  readonly params: Record<string, unknown>;
  readonly message?: string;
  /** With `verbose`: the failing keyword's value (a union's branches), its schema and the rejected value. */
  readonly schema?: unknown;
  readonly parentSchema?: unknown;
  readonly data?: unknown;
}

type JsonSchemaNode = Readonly<Record<string, unknown>>;

const isObject = (value: unknown): value is Readonly<Record<string, unknown>> =>
  typeof value === "object" && value !== null && !Array.isArray(value);
const nodes = (value: unknown): JsonSchemaNode[] => Array.isArray(value) ? value.filter(isObject) : [];
const branchesOf = (schema: JsonSchemaNode): JsonSchemaNode[] => [...nodes(schema.anyOf), ...nodes(schema.oneOf)];

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

/**
 * What one branch of a union accepts, as a rejection names it: its enumerated values, or its object
 * form with its members. `closed` is false for a branch that accepts any value of its type.
 */
function alternatives(branch: JsonSchemaNode): { readonly accepted: ArgumentValue[]; readonly closed: boolean } {
  if (Array.isArray(branch.enum)) return { accepted: scalars(branch.enum), closed: true };
  if (branch.const !== undefined) return { accepted: scalars([branch.const]), closed: true };
  if (branch.type === "object" || isObject(branch.properties)) {
    return { accepted: [`{${Object.keys(isObject(branch.properties) ? branch.properties : {}).join(", ")}}`], closed: true };
  }
  return { accepted: typeof branch.type === "string" ? [branch.type] : [], closed: false };
}

/** A valid value of a union as JSON: its first enumerated value, else its first object form's required members. */
function unionExample(branches: readonly JsonSchemaNode[]): string | undefined {
  for (const branch of branches) {
    const [value] = Array.isArray(branch.enum) ? scalars(branch.enum) : branch.const !== undefined ? scalars([branch.const]) : [];
    if (value !== undefined) return JSON.stringify(value);
  }
  const object = branches.find((branch) => isObject(branch.properties));
  if (object === undefined || !isObject(object.properties)) return undefined;
  const members = object.properties;
  const required = Array.isArray(object.required) ? object.required.filter((name): name is string => typeof name === "string") : [];
  const member = (name: string): string => {
    const schema = isObject(members[name]) ? members[name] : {};
    const [value] = Array.isArray(schema.enum) ? scalars(schema.enum) : schema.const !== undefined ? scalars([schema.const]) : [];
    return `${JSON.stringify(name)}: ${value === undefined ? "…" : JSON.stringify(value)}`;
  };
  return `{${(required.length > 0 ? required : Object.keys(members).slice(0, 1)).map(member).join(", ")}}`;
}

function describe(error: AjvError): ArgumentIssue {
  switch (error.keyword) {
    case "anyOf":
    case "oneOf": {
      // No branch was singled out (resolveUnions): name every alternative and a valid example.
      // The values first: a string is what a model most often means to send.
      const branches = nodes(error.schema).sort((left, right) => Number(isObject(left.properties)) - Number(isObject(right.properties)));
      const described = branches.map(alternatives);
      const accepted = described.flatMap((alternative) => alternative.accepted);
      const example = unionExample(branches);
      return {
        field: path(error.instancePath), keyword: error.keyword,
        message: accepted.length === 0 ? error.message ?? `fails ${error.keyword}` : `must be one of: ${list(accepted)}`,
        ...(accepted.length > 0 && described.every((alternative) => alternative.closed) ? { allowed: accepted } : {}),
        ...(example === undefined ? {} : { example }),
      };
    }
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
 * the one branch the value was meant for, else the union's own error, which names every
 * alternative. An object is meant for an object form: of several, the one whose discriminator
 * matched, or the one that knows every member it carries.
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
    const atUnion = (error: AjvError): boolean => error.instancePath === union.instancePath;
    let matching = perBranch.filter((errorsOfBranch) => !discriminatorMiss(errorsOfBranch));
    if (isObject(union.data)) {
      matching = matching.filter((errorsOfBranch) => !errorsOfBranch.some((error) => error.keyword === "type" && atUnion(error)));
      const knowing = matching.filter((errorsOfBranch) =>
        !errorsOfBranch.some((error) => error.keyword === "additionalProperties" && atUnion(error)));
      if (knowing.length > 0) matching = knowing;
    }
    // A value of none of the union's types: "must be string or object" says less than the values
    // and object forms of a union that enumerates them.
    const enumerated = nodes(union.schema).some((branch) => Array.isArray(branch.enum) || branch.const !== undefined);
    const keep = shared.length > 0 && !(enumerated && shared.every((error) => error.keyword === "type" && atUnion(error)))
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

/** The content of a JSON string literal sent as a string: `"\"rest\""` is `rest`. */
function quotedContent(value: string): string | undefined {
  if (value.length < 2 || !value.startsWith("\"") || !value.endsWith("\"")) return undefined;
  try {
    const content: unknown = JSON.parse(value);
    return typeof content === "string" ? content : undefined;
  } catch {
    return undefined;
  }
}

/** A schema and every branch nested in its unions. */
const forms = (schema: JsonSchemaNode): JsonSchemaNode[] => [schema, ...branchesOf(schema).flatMap(forms)];

/** The strings a schema accepts when they form a closed set; undefined when it accepts free text. */
function closedStrings(schema: JsonSchemaNode): string[] | undefined {
  if (Array.isArray(schema.enum)) return schema.enum.filter((value): value is string => typeof value === "string");
  if (schema.const !== undefined) return typeof schema.const === "string" ? [schema.const] : [];
  const branches = branchesOf(schema);
  if (branches.length > 0) {
    const sets = branches.map(closedStrings);
    return sets.some((set) => set === undefined) ? undefined : sets.flatMap((set) => set ?? []);
  }
  const types = Array.isArray(schema.type) ? schema.type : [schema.type];
  return types.includes("string") || (schema.type === undefined && !isObject(schema.properties)) ? undefined : [];
}

/** `value` with each enumerated string it carries in JSON quotes unquoted, copied only where that changed it. */
function unquoteClosedStrings(schema: JsonSchemaNode, value: unknown): unknown {
  if (typeof value === "string") {
    const content = quotedContent(value);
    const accepted = content === undefined ? undefined : closedStrings(schema);
    return content !== undefined && accepted?.includes(content) === true && !accepted.includes(value) ? content : value;
  }
  const candidates = forms(schema);
  if (Array.isArray(value)) {
    const items = candidates.map((node) => node.items).filter(isObject);
    const next = value.map((item) => items.reduce<unknown>((current, itemSchema) => unquoteClosedStrings(itemSchema, current), item));
    return next.some((item, index) => item !== value[index]) ? next : value;
  }
  if (!isObject(value)) return value;
  let next: Record<string, unknown> | undefined;
  for (const [name, member] of Object.entries(value)) {
    const unquoted = candidates
      .map((node) => isObject(node.properties) && Object.hasOwn(node.properties, name) ? node.properties[name] : undefined)
      .filter(isObject)
      .reduce<unknown>((current, memberSchema) => unquoteClosedStrings(memberSchema, current), member);
    if (unquoted !== member) (next ??= { ...value })[name] = unquoted;
  }
  return next ?? value;
}

/** The JSON Schema validator the generated documents are compiled with. */
class ContractValidator implements jsonSchemaValidator {
  /** `toolArguments`: a tool's arguments, whose enumerated strings may arrive in JSON quotes. */
  constructor(private readonly toolArguments: boolean) {}

  getValidator<T>(schema: JsonSchemaType): JsonSchemaValidator<T> {
    const validate = ajv.compile(schema);
    return (input) => {
      const data = this.toolArguments ? unquoteClosedStrings(schema as JsonSchemaNode, input) : input;
      if (validate(data)) return { valid: true, data: data as T, errorMessage: undefined };
      const issues = describeAjvErrors((validate.errors ?? []) as readonly AjvError[]);
      if (typeof input === "object" && input !== null) rejected.set(input, issues);
      return { valid: false, data: undefined, errorMessage: issues.map(formatArgumentIssue).join("; ") };
    };
  }
}

const contractValidator = new ContractValidator(false);
const argumentValidator = new ContractValidator(true);

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
    schema = fromJsonSchema<ContractDefinitions[Name]>(documents[name] as JsonSchemaType,
      name.startsWith("Mcp.Tools.") && name.endsWith("Input") ? argumentValidator : contractValidator);
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
