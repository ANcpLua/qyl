import { createTypeSpecLibrary, paramMessage } from "@typespec/compiler";

export const $lib = createTypeSpecLibrary({
  name: "@qyl/typespec-emit-ts-types",
  diagnostics: {
    "unmapped-type": {
      severity: "error",
      messages: {
        default: paramMessage`QYL-TSTY-001: unmapped type '${"name"}'`,
      },
    },
  },
  state: {
    tsBrand: { description: "Marks a scalar as a branded opaque string id" },
  },
} as const);

export const { reportDiagnostic, stateKeys } = $lib;
