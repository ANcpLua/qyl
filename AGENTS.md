# qyl

Engine: services/qyl.collector* (C#, NativeAOT). MCP: services/qyl.mcp (bun, MCP SDK v2). Contracts: contracts/ (TypeSpec). Check: ./eng/build.sh Check. Version: Version.props QylVersion, equal in services/qyl.mcp/package.json; ./release.sh releases it. Pushes to main deploy via .railway/railway.ts. After a SemanticConventions pin change: ./eng/build.sh GenerateCollectorSemanticAttributeCatalog. Layout and endpoints: README.md.
