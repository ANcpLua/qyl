import { createTypeSpecLibrary, paramMessage } from "@typespec/compiler";

export const $lib = createTypeSpecLibrary({
  name: "@qyl/typespec-emit-csharp",
  diagnostics: {
    "unmapped-type": {
      severity: "error",
      messages: {
        default: paramMessage`QYL-EMIT-001: unmapped type '${"name"}' — add a mapping or decorate the model with @csharpNamespace`,
      },
    },
    "missing-namespace": {
      severity: "error",
      messages: {
        default: paramMessage`QYL-EMIT-002: model '${"name"}' has no @csharpNamespace (inherited or direct)`,
      },
    },
  },
  state: {
    csharpNamespace: { description: "C# namespace override on a model/namespace/enum" },
    csharpRecord: { description: "Emit the target model as a C# record" },
    csharpEnum: { description: "Emit the target union/enum as a C# enum" },
  },
} as const);

export const { reportDiagnostic, createDiagnostic, stateKeys } = $lib;
