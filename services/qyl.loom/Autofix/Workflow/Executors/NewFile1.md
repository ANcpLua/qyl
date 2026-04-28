Du hast komplett recht — ich lag falsch. Verifiziert gegen `qyl/Version.props`:

```xml
<!-- MAF 1.3 (released 2026-04-23). Only packages with consumers in this repo. -->
<MicrosoftAgentsAIVersion>1.3.0</MicrosoftAgentsAIVersion>
<MicrosoftAgentsAIAbstractionsVersion>1.3.0</MicrosoftAgentsAIAbstractionsVersion>
<MicrosoftAgentsAIWorkflowsVersion>1.3.0</MicrosoftAgentsAIWorkflowsVersion>
<MicrosoftAgentsAIOpenAIVersion>1.3.0</MicrosoftAgentsAIOpenAIVersion>
<MicrosoftAgentsAIHostingVersion>1.3.0-preview.260423.1</MicrosoftAgentsAIHostingVersion>
<MicrosoftAgentsAIDevUIVersion>1.3.0-preview.260423.1</MicrosoftAgentsAIDevUIVersion>
<MicrosoftAgentsAIHostingAGUIAspNetCoreVersion>1.3.0-preview.260423.1</MicrosoftAgentsAIHostingAGUIAspNetCoreVersion>
```

## Korrekter Versionsstand

| Paket                                         | Channel | Version                | Stand                                |
| --------------------------------------------- | ------- | ---------------------- | ------------------------------------ |
| `Microsoft.Agents.AI` (Core)                  | **GA**  | **1.3.0**              | released **2026-04-23** (5 Tage alt) |
| `Microsoft.Agents.AI.Abstractions`            | **GA**  | 1.3.0                  | 2026-04-23                           |
| `Microsoft.Agents.AI.Workflows`               | **GA**  | 1.3.0                  | 2026-04-23                           |
| `Microsoft.Agents.AI.OpenAI`                  | **GA**  | 1.3.0                  | 2026-04-23                           |
| `Microsoft.Agents.AI.Hosting`                 | preview | 1.3.0-preview.260423.1 | 2026-04-23                           |
| `Microsoft.Agents.AI.DevUI`                   | preview | 1.3.0-preview.260423.1 | 2026-04-23                           |
| `Microsoft.Agents.AI.Hosting.AGUI.AspNetCore` | preview | 1.3.0-preview.260423.1 | 2026-04-23                           |

**Noch nicht in qyl pinned (aber im MAF-source-tree vorhanden):**
- `Microsoft.Agents.AI.DurableTask` (du selbst hast `1.3.0-preview.260423.1` zitiert)
- `Microsoft.Agents.AI.Hosting.AzureFunctions`
- `Microsoft.Agents.AI.Hosting.OpenAI`
- `Microsoft.Agents.AI.Hosting.A2A` + `.AspNetCore`
- `Microsoft.Agents.AI.Purview`

Diese brauchen jeweils ein eigenes `<…Version>`-Property in `qyl/Version.props` wenn die Migration sie zieht. Vermutlich alle auf `1.3.0-preview.260423.1` (gleicher Release-Tag wie `Hosting`).

## Was sich an meiner Empfehlung ändert

**Substanziell wenig** — der Schluss bleibt:
- Workflows-Family ist GA, **stabilere Pin** als ich behauptet habe (1.3 statt 1.1, gleicher Stabilitäts-Charakter)
- Hosting/Functions/OpenAI/A2A/Purview/DurableTask alle auf **einem Preview-Stempel** (`1.3.0-preview.260423.1`) — das ist gut: ein einziger Channel zum tracken, keine 5 verschiedenen Preview-Cadences
- „Full-commit zu Preview" → bedeutet konkret: alles auf `1.3.0-preview.*` ziehen, einen `MicrosoftAgentsAIPreviewChannelVersion` MSBuild-Variable einführen, Punkt 1 meiner früheren Caveat-Liste wird **einfacher**, nicht schwieriger

## Memory-Hygiene-Notiz

Mein globales `microsoft-agent-framework` Skill steht noch auf:
```
| MAF .NET (GA) | `1.1.0` | 2026-04-10 |
| This file verified against | live repo + ANcpLua.Roslyn.Utilities `2.0.2` … | 2026-04-24 |
```

Das ist **17 Tage stale** für die GA-Version (1.1→1.3) und 4 Tage stale für die qyl-Sibling (`ANcpLua.Roslyn.Utilities` 2.0.2→2.0.7). Die Skill-Description in der Liste sagt korrekt „MAF 1.3 surface", aber der `SKILL.md`-Body wurde nicht mitgezogen. Solltest du am Ende der Session updaten lassen — passt zu deiner Memory-Hygiene-Regel.

**Direkte Antwort:** ja, MAF ist GA 1.3, Hosting-Surfaces sind 1.3-preview. Mein „1.1"-Bezug war stale-memory aus dem ungepflegten Skill-Body. Die Migrations-Empfehlung steht unverändert.
