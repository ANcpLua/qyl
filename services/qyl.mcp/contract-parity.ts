import { readFile } from "node:fs/promises";
import { checkContract, type ContractName } from "./src/contracts/tool-schemas.js";
import { analysisHeadline } from "./src/analysis-summary.js";

interface Fixture {
  id: string;
  schema: ContractName;
  value: unknown;
}

const fixtures = JSON.parse(await readFile(process.argv[2]!, "utf8")) as Fixture[];
const results = fixtures.map(({ id, schema, value }) => {
  const result = checkContract(schema, value);
  const summary = result.valid && schema === "Mcp.Tools.TraceDivergence"
    ? analysisHeadline("compare_traces", {}, {
      comparable: true, first_divergence: result.value, aligned: [], after_divergence: [], unmatched: [],
    }) : undefined;
  return result.valid ? { id, valid: true, value: JSON.parse(JSON.stringify(result.value)) as unknown, summary }
    : { id, valid: false, message: result.message };
});
process.stdout.write(JSON.stringify(results));
