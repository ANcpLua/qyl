import fs from "node:fs/promises";
import path from "node:path";
import process from "node:process";
import {
  type ZodIssue,
  type ZodRawShape,
  type ZodTypeAny,
  type ZodTypeDef,
  z,
} from "zod";
import * as schemaModule from "./config/schema";

type ConfigLike = Record<string, unknown>;

export type DeepPartial<T> = T extends (...args: never[]) => unknown
  ? T
  : T extends Array<infer U>
    ? Array<DeepPartial<U>>
    : T extends ReadonlyArray<infer U>
      ? ReadonlyArray<DeepPartial<U>>
      : T extends Date
        ? Date
        : T extends object
          ? { [K in keyof T]?: DeepPartial<T[K]> }
          : T;

const isZodType = (value: unknown): value is ZodTypeAny => {
  return value instanceof z.ZodType;
};

const firstDefined = <T>(...values: Array<T | undefined>): T => {
  for (const value of values) {
    if (value !== undefined) return value;
  }

  throw new Error("Expected at least one defined value.");
};

const resolveSchema = <T>(module: Record<string, unknown>, names: string[]): T => {
  for (const name of names) {
    const candidate = module[name];
    if (isZodType(candidate)) {
      return candidate as T;
    }
  }

  throw new Error(`Unable to resolve schema among: ${names.join(", ")}`);
};

const pluginConfigSchema = resolveSchema<z.ZodTypeAny>(
  schemaModule as Record<string, unknown>,
  ["PluginConfigSchema", "pluginConfigSchema", "ConfigSchema", "configSchema", "PluginConfigurationSchema"],
);

const runtimeOverrideSchemaFromModule = (module: Record<string, unknown>): z.ZodTypeAny | null => {
  const names = [
    "PluginRuntimeOverrideSchema",
    "PluginRuntimeOverridesSchema",
    "RuntimeOverrideSchema",
    "RuntimeOverridesSchema",
    "ConfigOverrideSchema",
    "configOverrideSchema",
  ];

  for (const name of names) {
    const candidate = module[name];
    if (isZodType(candidate)) {
      return candidate as z.ZodTypeAny;
    }
  }

  return null;
};

const unwrapZodType = (schema: z.ZodTypeAny): z.ZodTypeAny => {
  let current: z.ZodTypeAny = schema;

  while (current instanceof z.ZodOptional || current instanceof z.ZodDefault || current instanceof z.ZodNullable || current instanceof z.ZodEffects) {
    if (current instanceof z.ZodOptional || current instanceof z.ZodDefault || current instanceof z.ZodNullable) {
      current = current._def.innerType;
      continue;
    }

    current = current._def.schema;
  }

  return current;
};

const toPartialShape = (schema: ZodTypeAny): ZodTypeAny => {
  const resolved = unwrapZodType(schema);

  if (resolved instanceof z.ZodObject) {
    const shape = resolved.shape as ZodRawShape;
    const partialShape: ZodRawShape = {};

    for (const [key, value] of Object.entries(shape)) {
      partialShape[key] = toPartialShape(value as ZodTypeAny).optional();
    }

    const partialObject = z.object(partialShape);
    const unknownPolicy = (resolved._def as ZodTypeDef & { unknownKeys?: "passthrough" | "strict" | "strip" }).unknownKeys;

    if (unknownPolicy === "passthrough") {
      return partialObject.passthrough();
    }

    if (unknownPolicy === "strip") {
      return partialObject.strip();
    }

    return partialObject;
  }

  if (resolved instanceof z.ZodArray) {
    return z.array(toPartialShape(resolved._def.type));
  }

  if (resolved instanceof z.ZodRecord) {
    return z.record(resolved._def.keyType, toPartialShape(resolved._def.valueType));
  }

  if (resolved instanceof z.ZodTuple) {
    return z.tuple(resolved._def.items.map((item: ZodTypeAny) => toPartialShape(item) as any));
  }

  if (resolved instanceof z.ZodUnion) {
    return z.union(resolved._def.options.map((option: ZodTypeAny) => toPartialShape(option)));
  }

  if (resolved instanceof z.ZodDiscriminatedUnion) {
    return z.discriminatedUnion(
      resolved._def.discriminator,
      resolved._def.options.map((option: ZodTypeAny) => toPartialShape(option) as any),
    );
  }

  return schema;
};

const pluginRuntimeOverrideSchema = firstDefined(
  runtimeOverrideSchemaFromModule(schemaModule as Record<string, unknown>),
  toPartialShape(pluginConfigSchema),
);

export type PluginConfig = z.infer<typeof pluginConfigSchema>;
export type PluginConfigPatch = DeepPartial<PluginConfig>;

export type PartialParseOptions = {
  partial?: true;
};

export type FullParseOptions = {
  partial?: false;
};

export type RuntimeOverridesInput =
  | string
  | number
  | boolean
  | null
  | ConfigLike
  | Partial<Record<string, unknown>>;

export type MergeRuntimeOverridesInput = {
  [k: string]: RuntimeOverridesInput | undefined;
};

export type LoadPluginConfigOptions = {
  cwd?: string;
  path?: string;
  raw?: unknown;
  partial?: boolean;
  runtimeOverrides?: MergeRuntimeOverridesInput;
  parseJson?: boolean;
};

export type ConfigParseError = {
  code: "config_parse_error";
  issues: ZodIssue[];
  schemaPath?: string;
};

export class PluginConfigError extends Error {
  public readonly issues?: readonly ZodIssue[];
  public readonly schemaPath: string | undefined;

  public constructor(message: string, issues?: readonly ZodIssue[], schemaPath?: string) {
    super(message);
    this.name = "PluginConfigError";
    this.issues = issues;
    this.schemaPath = schemaPath;
  }
}

const isPlainObject = (value: unknown): value is ConfigLike => {
  return typeof value === "object" && value !== null && !Array.isArray(value);
};

const withIssueString = (issue: ZodIssue): string => {
  const path = issue.path.length > 0 ? issue.path.join(".") : "<root>";
  return `${path}: [${issue.code}] ${issue.message}`;
};

const parseSchema = <T>(schema: z.ZodTypeAny, raw: unknown, schemaPath: string): T => {
  const result = schema.safeParse(raw);
  if (!result.success) {
    const issueSummary = result.error.issues
      .slice(0, 6)
      .map(withIssueString)
      .join("; ");
    throw new PluginConfigError(
      `Configuration validation failed for ${schemaPath}: ${issueSummary}`,
      result.error.issues,
      schemaPath,
    );
  }

  return result.data as T;
};

const parseJson = (raw: string): unknown => {
  try {
    return JSON.parse(raw);
  } catch (error) {
    throw new PluginConfigError(`Failed to parse config JSON: ${(error as Error).message}`);
  }
};

const normalizeOverrideValue = (input: RuntimeOverridesInput): RuntimeOverridesInput => {
  if (typeof input !== "string") return input;

  const lowered = input.trim().toLowerCase();
  if (lowered === "true") return true;
  if (lowered === "false") return false;
  if (lowered === "null") return null;
  if (/^-?\d+(?:\.\d+)?$/.test(input.trim())) {
    return Number(input);
  }

  if (input.startsWith("{") || input.startsWith("[") || (input.startsWith("\"") && input.endsWith("\""))) {
    try {
      return JSON.parse(input);
    } catch {
      return input;
    }
  }

  return input;
};

const setByPath = (target: ConfigLike, pathParts: string[], value: unknown): ConfigLike => {
  const [head, ...rest] = pathParts;
  if (!head) return target;

  const current = target[head];
  if (rest.length === 0) {
    return {
      ...target,
      [head]: value,
    };
  }

  const nextTarget = isPlainObject(current)
    ? current
    : {};

  return {
    ...target,
    [head]: setByPath(nextTarget, rest, value),
  };
};

const fromDottedOverrides = (raw: MergeRuntimeOverridesInput): ConfigLike => {
  const next: ConfigLike = {};

  for (const [rawKey, rawValue] of Object.entries(raw)) {
    if (rawValue === undefined) {
      continue;
    }

    const normalizedKey = rawKey.replace(/__/g, ".");
    const pathParts = normalizedKey.split(".").filter((segment) => segment.length > 0);
    const normalizedValue = normalizeOverrideValue(rawValue);

    Object.assign(next, setByPath(next, pathParts, normalizedValue));
  }

  return next;
};

const mergeLeaf = (left: unknown, right: unknown): unknown => {
  if (Array.isArray(right)) {
    return [...right];
  }

  if (isPlainObject(right) && isPlainObject(left)) {
    return mergeObjects(left, right);
  }

  return right;
};

const mergeObjects = (left: ConfigLike, right: ConfigLike): ConfigLike => {
  const result: ConfigLike = { ...left };

  for (const [key, rightValue] of Object.entries(right)) {
    const leftValue = result[key];
    if (rightValue === undefined) {
      continue;
    }

    result[key] = mergeLeaf(leftValue, rightValue);
  }

  return result;
};

export const deepMerge = <T>(base: T, patch: T): T => {
  if (base === null || patch === null) return patch;
  if (!isPlainObject(base) || !isPlainObject(patch)) {
    return patch;
  }

  return mergeObjects(base, patch) as T;
};

export const parsePluginConfig = <T extends boolean = false>(raw: unknown, options: { partial?: T } = {}): T extends true
  ? PluginConfigPatch
  : PluginConfig => {
  const schema = options.partial
    ? toPartialShape(pluginConfigSchema)
    : pluginConfigSchema;

  return parseSchema<T extends true ? PluginConfigPatch : PluginConfig>(schema as z.ZodTypeAny, raw, options.partial ? "PluginConfigSchema(partial)" : "PluginConfigSchema");
};

export const parseRuntimeOverrides = (raw: MergeRuntimeOverridesInput | ConfigLike): PluginConfigPatch => {
  const asObj: ConfigLike = isPlainObject(raw)
    ? raw as ConfigLike
    : fromDottedOverrides(raw);

  return parseSchema<PluginConfigPatch>(pluginRuntimeOverrideSchema, asObj, "RuntimeOverrideSchema");
};

export const applyRuntimeOverrides = <T extends PluginConfig>(config: T, overrides: MergeRuntimeOverridesInput | ConfigLike): T => {
  const normalized = isPlainObject(overrides) ? overrides as ConfigLike : fromDottedOverrides(overrides);
  const validated = parseRuntimeOverrides(normalized as MergeRuntimeOverridesInput);

  return deepMerge(config, validated as T);
};

export const loadPluginConfig = async (
  options: LoadPluginConfigOptions,
): Promise<PluginConfig | PluginConfigPatch> => {
  const {
    cwd = process.cwd(),
    path: maybePath,
    raw,
    partial = false,
    runtimeOverrides,
    parseJson: shouldParseJson = true,
  } = options;

  const source = raw === undefined
    ? await (async () => {
      if (!maybePath) {
        throw new Error("loadPluginConfig requires 'path' when 'raw' is not supplied.");
      }

      const resolvedPath = path.resolve(cwd, maybePath);
      const fileContents = await fs.readFile(resolvedPath, "utf-8");
      return shouldParseJson ? parseJson(fileContents) : fileContents;
    })()
    : raw;

  const parsed = parsePluginConfig(source, { partial });

  if (!runtimeOverrides || Object.keys(runtimeOverrides).length === 0) {
    return parsed;
  }

  return applyRuntimeOverrides(parsed as PluginConfig, runtimeOverrides);
};

export const loadConfigFromPath = (
  filePath: string,
  options: Omit<LoadPluginConfigOptions, "path" | "raw"> = {},
): Promise<PluginConfig | PluginConfigPatch> => {
  return loadPluginConfig({
    ...options,
    path: filePath,
  });
};
