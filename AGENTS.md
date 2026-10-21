# qyl

Engine: services/qyl.collector* (C#, NativeAOT, DuckDB). MCP: services/qyl.mcp (Bun, MCP TypeScript SDK v2). Contracts: contracts/ (TypeSpec); regenerate with ./eng/build.sh Contracts. Before every push: ./eng/build.sh Check --Configuration Release. Version: Version.props QylVersion, the same in services/qyl.mcp/package.json; ./release.sh releases it. After a SemanticConventions pin change: ./eng/build.sh GenerateCollectorSemanticAttributeCatalog. More: README.md.
