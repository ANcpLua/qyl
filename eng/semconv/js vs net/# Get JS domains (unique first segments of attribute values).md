/tmp/otel-semconv-compare/analyze.js)
⎿ Wrote 45 lines to /tmp/otel-semconv-compare/analyze.js
1 const sc = require("@opentelemetry/semantic-conventions");
2 const keys = Object.keys(sc);
3 const stableAttrs = [];
4 const legacyAttrs = [];
5 const metricNames = [];
6 const enumValues = [];
7 keys.forEach(k => {
8   const v = sc[k];
9   if (typeof v === "object") { enumValues.push(k); return; }
10   if (typeof v !== "string") return;
11   if (k.startsWith("ATTR_")) stableAttrs.push({key: k, val: v});
12   else if (k.startsWith("SEMATTRS_") || k.startsWith("SEMRESATTRS_")) legacyAttrs.push({key: k, val: v});
13   else if (k.startsWith("METRIC_")) metricNames.push({key: k, val: v});
14 });
15 console.log("STABLE attrs (ATTR_*):", stableAttrs.length);
16 console.log("LEGACY attrs (SEMATTRS_*/SEMRESATTRS_*):", legacyAttrs.length);
17 console.log("METRICS (METRIC_*):", metricNames.length);
18 console.log("Enum objects:", enumValues.length);
19
20 const stableDomains = {};
21 stableAttrs.forEach(a => { const d = a.val.split(".")[0]; stableDomains[d] = (stableDomains[d]||0)+1; });
22 console.log("\n=== STABLE ATTR DOMAINS ===");
23 Object.entries(stableDomains).sort().forEach(([d,c]) => console.log(d+":", c));
24
25 const metricDomains = {};
26 metricNames.forEach(m => { const d = m.val.split(".")[0]; metricDomains[d] = (metricDomains[d]||0)+1; });
27 console.log("\n=== METRIC DOMAINS ===");
28 Object.entries(metricDomains).sort().forEach(([d,c]) => console.log(d+":", c));
29
30 console.log("\n=== GenAI attrs ===");
31 keys.filter(k => { const v = sc[k]; return typeof v === "string" && v.includes("gen_ai"); }).forEach(k => console.log(k, "=", sc[k]));
32
33 console.log("\n=== .NET-specific attrs ===");
34 keys.filter(k => { const v = sc[k]; return typeof v === "string" && (v.startsWith("dotnet.") || v.startsWith("aspnetcore.") || v.startsWith("signalr.")); }).forEach(k => console.log(k, "=", sc[k]));
35
36 console.log("\n=== JVM-specific attrs ===");
37 keys.filter(k => { const v = sc[k]; return typeof v === "string" && v.startsWith("jvm."); }).forEach(k => console.log(k, "=", sc[k]));
38
39 // All unique attribute value strings for cross-comparison
40 const allVals = new Set();
41 stableAttrs.forEach(a => allVals.add(a.val));
42 legacyAttrs.forEach(a => allVals.add(a.val));
43 const sorted = [...allVals].sort();
44 console.log("\n=== ALL ATTRIBUTE VALUE STRINGS (" + sorted.length + ") ===");
45 sorted.forEach(v => console.log(v));
⎿ PostToolUse:Write hook error
⎿ PostToolUse:Write hook error
⎿ ../../../../tmp/otel-semconv-compare/analyze.js (/tmp/otel-semconv-compare/analyze.js):
⎿   ★ [Line 1:12] File is a CommonJS module; it may be converted to an ES module. [80001] (typescript)
