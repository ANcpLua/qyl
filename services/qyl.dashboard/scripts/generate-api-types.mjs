import { mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join, resolve } from "node:path";
import { spawn } from "node:child_process";

const defaultOpenApiPath = "../../../qyl-api-schema/generated/openapi/qyl.openapi.json";
const source = resolve(process.cwd(), process.env.QYL_API_SCHEMA_OPENAPI ?? defaultOpenApiPath);
const output = resolve(process.cwd(), "src/types/api.ts");
const workDir = await mkdtemp(join(tmpdir(), "qyl-openapi-"));

try {
  const normalized = join(workDir, "qyl.openapi.json");
  const spec = JSON.parse(await readFile(source, "utf8"));
  if (spec.openapi === "3.2.0") {
    spec.openapi = "3.1.0";
  }
  await writeFile(normalized, `${JSON.stringify(spec, null, 2)}\n`, "utf8");
  await run("openapi-typescript", [normalized, "-o", output]);
} finally {
  await rm(workDir, { recursive: true, force: true });
}

function run(command, args) {
  return new Promise((resolveRun, reject) => {
    const child = spawn(command, args, { stdio: "inherit", shell: process.platform === "win32" });
    child.on("error", reject);
    child.on("exit", (code) => {
      if (code === 0) resolveRun();
      else reject(new Error(`${command} exited with ${code}`));
    });
  });
}
