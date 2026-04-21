import type { DecoratorContext, Model, ModelProperty, Type } from "@typespec/compiler";
import { stateKeys } from "./lib.js";

export function $duckdbTable(context: DecoratorContext, target: Model, name: string): void {
  context.program.stateMap(stateKeys.duckdbTable).set(target, name);
}

export function $duckdbColumn(context: DecoratorContext, target: ModelProperty, type?: string): void {
  context.program.stateMap(stateKeys.duckdbColumn).set(target, type ?? "");
}

export function $duckdbPrimaryKey(context: DecoratorContext, target: ModelProperty): void {
  context.program.stateMap(stateKeys.duckdbPrimaryKey).set(target, true);
}

export function $duckdbIndex(context: DecoratorContext, target: ModelProperty, name: string): void {
  const map = context.program.stateMap(stateKeys.duckdbIndex);
  const list = (map.get(target) as string[] | undefined) ?? [];
  list.push(name);
  map.set(target, list);
}

export function getTableName(program: { stateMap: (k: symbol) => Map<Type, unknown> }, target: Type): string | undefined {
  return program.stateMap(stateKeys.duckdbTable).get(target) as string | undefined;
}

export function getColumnTypeOverride(program: { stateMap: (k: symbol) => Map<Type, unknown> }, target: Type): string | undefined {
  const v = program.stateMap(stateKeys.duckdbColumn).get(target);
  return typeof v === "string" && v.length > 0 ? v : undefined;
}

export function isPrimaryKey(program: { stateMap: (k: symbol) => Map<Type, unknown> }, target: Type): boolean {
  return program.stateMap(stateKeys.duckdbPrimaryKey).has(target);
}

export function getIndexes(program: { stateMap: (k: symbol) => Map<Type, unknown> }, target: Type): string[] {
  return (program.stateMap(stateKeys.duckdbIndex).get(target) as string[] | undefined) ?? [];
}
