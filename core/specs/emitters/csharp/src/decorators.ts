import type { DecoratorContext, Enum, Model, Namespace, Type, Union } from "@typespec/compiler";
import { stateKeys } from "./lib.js";

export function $csharpNamespace(
  context: DecoratorContext,
  target: Model | Enum | Namespace,
  ns: string,
): void {
  context.program.stateMap(stateKeys.csharpNamespace).set(target, ns);
}

export function $csharpRecord(context: DecoratorContext, target: Model): void {
  context.program.stateMap(stateKeys.csharpRecord).set(target, true);
}

export function $csharpEnum(context: DecoratorContext, target: Enum | Union): void {
  context.program.stateMap(stateKeys.csharpEnum).set(target, true);
}

export function getCsharpNamespace(program: { stateMap: (k: symbol) => Map<Type, unknown> }, target: Type): string | undefined {
  return program.stateMap(stateKeys.csharpNamespace).get(target) as string | undefined;
}

export function hasCsharpRecord(program: { stateMap: (k: symbol) => Map<Type, unknown> }, target: Type): boolean {
  return program.stateMap(stateKeys.csharpRecord).has(target);
}

export function hasCsharpEnum(program: { stateMap: (k: symbol) => Map<Type, unknown> }, target: Type): boolean {
  return program.stateMap(stateKeys.csharpEnum).has(target);
}
