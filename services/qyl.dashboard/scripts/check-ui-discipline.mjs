// oxlint has no no-restricted-syntax, so the two JSX-level Base UI guardrails the
// eslint config enforced live here (the @radix-ui import ban stayed in .oxlintrc.json).
import fs from "node:fs";
import path from "node:path";

const root = new URL("../src", import.meta.url).pathname;
const rules = [
  { pattern: /\basChild\b/, message: "Use Base UI render composition instead of Radix-style asChild." },
  { pattern: /<Slot[\s/>]/, message: "Use Base UI render composition instead of Slot." },
];

const violations = [];
const walk = (dir) => {
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) walk(full);
    else if (/\.(ts|tsx)$/.test(entry.name)) {
      const lines = fs.readFileSync(full, "utf8").split("\n");
      lines.forEach((line, i) => {
        for (const rule of rules)
          if (rule.pattern.test(line)) violations.push(`${full}:${i + 1} ${rule.message}`);
      });
    }
  }
};
walk(root);

if (violations.length > 0) {
  console.error(violations.join("\n"));
  process.exit(1);
}
console.log("ui discipline: no asChild/Slot usage in src");
