import type {
  RunnerLogLine,
  RunnerResourceState,
} from "@ancplua/qyl-api-schema/types";
import qylSchema from "@ancplua/qyl-api-schema/json-schema" with { type: "json" };
import Ajv2020, { type ValidateFunction } from "ajv/dist/2020.js";

const validator = new Ajv2020({ strict: false, validateFormats: false });
validator.addSchema(qylSchema);

function compile<T>(definition: string): ValidateFunction<T> {
  return validator.compile<T>({ $ref: `${qylSchema.$id}#/$defs/${definition}` });
}

const validateResourceState = compile<RunnerResourceState>("Runner.RunnerResourceState");
const validateLogLine = compile<RunnerLogLine>("Runner.RunnerLogLine");

function parseContract<T>(contract: ValidateFunction<T>, value: unknown, context: string): T {
  if (contract(value)) return value;
  throw new Error(
    `Runner contract mismatch for ${context}: ${validator.errorsText(contract.errors, {
      separator: "; ",
      dataVar: context,
    })}`,
  );
}

export const parseResourceState = (value: unknown): RunnerResourceState =>
  parseContract(validateResourceState, value, "resource state SSE frame");

export const parseLogLine = (value: unknown): RunnerLogLine =>
  parseContract(validateLogLine, value, "resource log SSE frame");
