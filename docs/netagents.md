 e## netagents — What It Is

A **.NET package manager + compile-time MCP server source generator**. Two halves:

1. **CLI (`netagents`)** — manages reusable AI tool packages ("skills") across agent tools (Claude Code, Cursor, Codex, VS Code). Commands: `init`, `install`, `add`, `remove`, `sync`, `list`. Writes agent-specific config files so one skill definition works everywhere.

2. **Source generator (`Qyl.Agents.Generator`)** — you mark a class with `[McpServer]` and methods with `[Tool]`, and at compile time it generates: dispatch routing, JSON schemas, OTel spans, metadata, and serialization context. Zero runtime reflection.

---

## Usage in qyl (`/Users/ancplua/qyl`)

**Limited but strategic** — only `qyl.loom` uses it:

- **Packages**: `Qyl.Agents`, `Qyl.Agents.Abstractions`, `Qyl.Agents.Generator` all at v0.2.0
- **Single MCP server**: `LoomGodAnalyzerServer` in `src/qyl.loom/Agents/` — decorated with `[McpServer]`, `[Tool]`, and `[Prompt]` attributes
- **3 tools**: `loom_get_issue_insight`, `loom_start_fix_run`, `loom_review_pull_request`
- **Hosting**: `LoomGodAnalyzerHostingExtensions.cs` uses `Qyl.Agents.Hosting`
- No `agents.toml`/`agents.lock` — the CLI package management side isn't used, only the source generator

---

## Usage in qyl.mcp (`/Users/ancplua/qyl.mcp`)

**None.** qyl.mcp has a completely independent stack:

- Uses `ModelContextProtocol` NuGet package with `[McpServerToolType]` / `[McpServerTool]` attributes (not Qyl.Agents)
- Has its own custom Roslyn source generator (`qyl.mcp.generators/ToolManifestGenerator.cs`) that emits a `QylToolManifest` class
- 77 tool files use this separate pattern
- Zero references to Qyl.Agents anywhere

---

I ASSUME netagents located in /Users/ancplua/netagents/src/NetAgents
/Users/ancplua/netagents/src/Qyl.Agents
/Users/ancplua/netagents/src/Qyl.Agents.Abstractions
/Users/ancplua/netagents/src/Qyl.Agents.Generator
/Users/ancplua/netagents/src/Qyl.ChatKit should power the qyl.loom analyze codebase feature tool(if its not built or named differnelty that was the plan as anyone asking whats the code issue we let this monster inmemory create skills, mcpservers, tools with /Users/ancplua/qyl.mcp/src/qyl.mcp.generators/ToolManifestGenerator.cs
/Users/ancplua/qyl.mcp/src/qyl.mcp.generators/Models/ToolManifestModels.cs
/Users/ancplua/qyl.mcp/src/qyl.mcp.generators/Emitters/ToolManifestEmitter.cs
/Users/ancplua/qyl.mcp/src/qyl.mcp.generators/Models
/Users/ancplua/qyl.mcp/src/qyl.mcp.generators/Emitters
/Users/ancplua/qyl.mcp/src/qyl.mcp.generators/Analyzers/ToolManifestAnalyzer.cs
/Users/ancplua/qyl.mcp/src/qyl.mcp.generators/Analyzers
/Users/ancplua/qyl.mcp/src/qyl.mcp.generators/qyl.mcp.generators.csproj


) call from qyl.mcp via source generation but qyl.mcp doesn't use its AOT ment migration codebase  at all — it has its own parallel tool system built on the official ModelContextProtocol SDK. from


I have located the current source code of MAF as of 5th april at the following location path:
/Users/ancplua/agent-framework/dotnet
Microsoft Agent Framework → unified runtime + workflows + state + enterprise system
also called MAF which absorbed:
AutoGen        → agent interaction (HOW agents talk)
Semantic Kernel → agent capabilities (WHAT agents can do)

AutoGen had:
flexibility focus ↑ UP
chaos focus ↑ UP
control focus ↓ DOWN

Semantic Kernel world:
structure focus ↑ UP
enterprise focus ↑ UP
freedom focus ↓ DOWN

Bonus mentions in eng and tests:

- [eng/verify-samples/WorkflowSamples.cs](/Users/ancplua/agent-framework/dotnet/eng/verify-samples/WorkflowSamples.cs)
- [eng/verify-samples/AgentsSamples.cs](/Users/ancplua/agent-framework/dotnet/eng/verify-samples/AgentsSamples.cs)
- [tests/Microsoft.Agents.AI.Workflows.Declarative.Mcp.UnitTests/DefaultMcpToolHandlerTests.cs](/Users/ancplua/agent-framework/dotnet/tests/Microsoft.Agents.AI.Workflows.Declarative.Mcp.UnitTests/DefaultMcpToolHandlerTests.cs)
- [tests/Microsoft.Agents.AI.Workflows.Declarative.Mcp.UnitTests/Microsoft.Agents.AI.Workflows.Declarative.Mcp.UnitTests.csproj](/Users/ancplua/agent-framework/dotnet/tests/Microsoft.Agents.AI.Workflows.Declarative.Mcp.UnitTests/Microsoft.Agents.AI.Workflows.Declarative.Mcp.UnitTests.csproj)
- [tests/Microsoft.Agents.AI.Hosting.AzureFunctions.UnitTests/DurableAgentFunctionMetadataTransformerTests.cs](/Users/ancplua/agent-framework/dotnet/tests/Microsoft.Agents.AI.Hosting.AzureFunctions.UnitTests/DurableAgentFunctionMetadataTransformerTests.cs)
- [tests/Microsoft.Agents.AI.Hosting.AzureFunctions.UnitTests/FunctionMetadataFactoryTests.cs](/Users/ancplua/agent-framework/dotnet/tests/Microsoft.Agents.AI.Hosting.AzureFunctions.UnitTests/FunctionMetadataFactoryTests.cs)
- [tests/Microsoft.Agents.AI.Hosting.AzureFunctions.IntegrationTests/WorkflowSamplesValidation.cs](/Users/ancplua/agent-framework/dotnet/tests/Microsoft.Agents.AI.Hosting.AzureFunctions.IntegrationTests/WorkflowSamplesValidation.cs)
- [tests/Microsoft.Agents.AI.Hosting.AzureFunctions.IntegrationTests/Microsoft.Agents.AI.Hosting.AzureFunctions.IntegrationTests.csproj](/Users/ancplua/agent-framework/dotnet/tests/Microsoft.Agents.AI.Hosting.AzureFunctions.IntegrationTests/Microsoft.Agents.AI.Hosting.AzureFunctions.IntegrationTests.csproj)
- [tests/Microsoft.Agents.AI.Hosting.AzureFunctions.IntegrationTests/SamplesValidation.cs](/Users/ancplua/agent-framework/dotnet/tests/Microsoft.Agents.AI.Hosting.AzureFunctions.IntegrationTests/SamplesValidation.cs)
- [tests/Microsoft.Agents.AI.GitHub.Copilot.IntegrationTests/GitHubCopilotAgentTests.cs](/Users/ancplua/agent-framework/dotnet/tests/Microsoft.Agents.AI.GitHub.Copilot.IntegrationTests/GitHubCopilotAgentTests.cs)
- [tests/Microsoft.Agents.AI.Workflows.Declarative.IntegrationTests/InvokeToolWorkflowTest.cs](/Users/ancplua/agent-framework/dotnet/tests/Microsoft.Agents.AI.Workflows.Declarative.IntegrationTests/InvokeToolWorkflowTest.cs)
- [tests/Microsoft.Agents.AI.Workflows.Declarative.IntegrationTests/Workflows/InvokeMcpToolWithApproval.yaml](/Users/ancplua/agent-framework/dotnet/tests/Microsoft.Agents.AI.Workflows.Declarative.IntegrationTests/Workflows/InvokeMcpToolWithApproval.yaml)
- [tests/Microsoft.Agents.AI.Workflows.Declarative.IntegrationTests/Workflows/InvokeMcpTool.yaml](/Users/ancplua/agent-framework/dotnet/tests/Microsoft.Agents.AI.Workflows.Declarative.IntegrationTests/Workflows/InvokeMcpTool.yaml)
- [tests/Microsoft.Agents.AI.Workflows.Declarative.IntegrationTests/Framework/IntegrationTest.cs](/Users/ancplua/agent-framework/dotnet/tests/Microsoft.Agents.AI.Workflows.Declarative.IntegrationTests/Framework/IntegrationTest.cs)
- [tests/Microsoft.Agents.AI.Workflows.Declarative.IntegrationTests/Microsoft.Agents.AI.Workflows.Declarative.IntegrationTests.csproj](/Users/ancplua/agent-framework/dotnet/tests/Microsoft.Agents.AI.Workflows.Declarative.IntegrationTests/Microsoft.Agents.AI.Workflows.Declarative.IntegrationTests.csproj)
- [tests/Microsoft.Agents.AI.GitHub.Copilot.UnitTests/GitHubCopilotAgentTests.cs](/Users/ancplua/agent-framework/dotnet/tests/Microsoft.Agents.AI.GitHub.Copilot.UnitTests/GitHubCopilotAgentTests.cs)
- [tests/Microsoft.Agents.AI.Foundry.UnitTests/AzureAIProjectChatClientExtensionsTests.cs](/Users/ancplua/agent-framework/dotnet/tests/Microsoft.Agents.AI.Foundry.UnitTests/AzureAIProjectChatClientExtensionsTests.cs)
- [tests/Microsoft.Agents.AI.Foundry.UnitTests/TestDataUtil.cs](/Users/ancplua/agent-framework/dotnet/tests/Microsoft.Agents.AI.Foundry.UnitTests/TestDataUtil.cs)
- [tests/Microsoft.Agents.AI.Workflows.Declarative.UnitTests/Events/ExternalInputResponseTest.cs](/Users/ancplua/agent-framework/dotnet/tests/Microsoft.Agents.AI.Workflows.Declarative.UnitTests/Events/ExternalInputResponseTest.cs)
- [tests/Microsoft.Agents.AI.Workflows.Declarative.UnitTests/Events/ExternalInputRequestTest.cs](/Users/ancplua/agent-framework/dotnet/tests/Microsoft.Agents.AI.Workflows.Declarative.UnitTests/Events/ExternalInputRequestTest.cs)
- [tests/Microsoft.Agents.AI.Workflows.Declarative.UnitTests/ObjectModel/InvokeMcpToolExecutorTest.cs](/Users/ancplua/agent-framework/dotnet/tests/Microsoft.Agents.AI.Workflows.Declarative.UnitTests/ObjectModel/InvokeMcpToolExecutorTest.cs)
- [tests/Microsoft.Agents.AI.Workflows.UnitTests/WorkflowHostSmokeTests.cs](/Users/ancplua/agent-framework/dotnet/tests/Microsoft.Agents.AI.Workflows.UnitTests/WorkflowHostSmokeTests.cs)
- [tests/Microsoft.Agents.AI.Declarative.UnitTests/AgentBotElementYamlTests.cs](/Users/ancplua/agent-framework/dotnet/tests/Microsoft.Agents.AI.Declarative.UnitTests/AgentBotElementYamlTests.cs)
- [tests/Microsoft.Agents.AI.Declarative.UnitTests/PromptAgents.cs](/Users/ancplua/agent-framework/dotnet/tests/Microsoft.Agents.AI.Declarative.UnitTests/PromptAgents.cs)
