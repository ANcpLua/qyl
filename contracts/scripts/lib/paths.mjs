// Where the contract's generated faces live. Paths resolve from this file, so every script
// writes the same files whatever directory it runs from.
import { readFile } from "node:fs/promises";
import { fileURLToPath } from "node:url";

const repository = (path) => fileURLToPath(new URL(`../../../${path}`, import.meta.url));

/** The emitted OpenAPI document, committed. */
export const openApiPath = repository("contracts/openapi/qyl.openapi.json");

/** The C# contracts: the emitter writes Qyl/; the project file beside it is not generated. */
export const csharpGeneratedDirectory = repository("internal/qyl.contracts/Qyl");
export const csharpRevisionPath = repository("internal/qyl.contracts/Qyl/Api/ContractRevision.cs");

/** The MCP's generated module: TypeScript types, header names and tool schema documents. */
export const mcpGeneratedDirectory = repository("services/qyl.mcp/src/contracts/generated");
export const mcpApiPath = repository("services/qyl.mcp/src/contracts/generated/api.ts");
export const mcpHeadersPath = repository("services/qyl.mcp/src/contracts/generated/headers.ts");
export const mcpToolSchemasPath = repository("services/qyl.mcp/src/contracts/generated/tool-schemas.json");

export async function readOpenApi() {
  return JSON.parse(await readFile(openApiPath, "utf8"));
}
