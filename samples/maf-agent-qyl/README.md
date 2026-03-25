# MAF: Lean Loom Subsystem Sample

This sample replaces the old "large prompt truncation" demo with a cleaner proof:

- register bounded Loom agents with `AddAIAgent(...)`
- attach hosted durability with `WithInMemorySessionStore()`
- attach a real tool with `WithAITool(...)`
- keep **Loom-owned handoff state explicit**

That last point is the important one. The sample does **not** pretend that one shared conversation ID gives multiple
agents a magical shared memory. Diagnostician, Strategist, Coder, Reviewer, and Librarian are registered through
Microsoft Agent Framework hosting extensions, but Loom still owns the cross-agent state transitions.

## What It Shows

- `loom.diagnostician` streams a production root-cause analysis
- `loom.strategist` turns that into the minimal safe fix plan
- `loom.coder` uses a registered `CreatePullRequest` tool
- `loom.reviewer` reviews the generated patch summary
- `loom.librarian` clusters the incident into a failure family

Everything lives in one file: [Program.cs](./Program.cs).

## Run It

```bash
dotnet run --project samples/maf-agent-qyl
```

The sample is intentionally lean: no AG-UI host, no declarative workflow engine, no qyl collector dependency.
It proves the hosting extension path first.
