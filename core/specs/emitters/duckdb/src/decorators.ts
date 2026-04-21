import type { DecoratorContext, Model, ModelProperty, Scalar, Type } from "@typespec/compiler";
import { stateKeys } from "./lib.js";

export function $duckdbTable(context: DecoratorContext, target: Model, name: string): void {
  context.program.stateMap(stateKeys.duckdbTable).set(target, name);
}

export function $duckdbColumn(context: DecoratorContext, target: ModelProperty, name: string): void {
  context.program.stateMap(stateKeys.duckdbColumn).set(target, name);
}

export function $duckdbType(context: DecoratorContext, target: ModelProperty | Scalar, type: string): void {
  context.program.stateMap(stateKeys.duckdbType).set(target, type);
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

export function $duckdbRowTimestamp(context: DecoratorContext, target: Model): void {
  context.program.stateMap(stateKeys.duckdbRowTimestamp).set(target, true);
}

type StateReader = { stateMap: (k: symbol) => Map<Type, unknown> };

export function getTableName(program: StateReader, target: Type): string | undefined {
  return program.stateMap(stateKeys.duckdbTable).get(target) as string | undefined;
}

export function getColumnName(program: StateReader, target: Type): string | undefined {
  return program.stateMap(stateKeys.duckdbColumn).get(target) as string | undefined;
}

export function getColumnTypeOverride(program: StateReader, target: Type): string | undefined {
  return program.stateMap(stateKeys.duckdbType).get(target) as string | undefined;
}

export function isPrimaryKey(program: StateReader, target: Type): boolean {
  return program.stateMap(stateKeys.duckdbPrimaryKey).has(target);
}

export function getIndexes(program: StateReader, target: Type): string[] {
  return (program.stateMap(stateKeys.duckdbIndex).get(target) as string[] | undefined) ?? [];
}

export function hasRowTimestamp(program: StateReader, target: Type): boolean {
  return program.stateMap(stateKeys.duckdbRowTimestamp).has(target);
}
