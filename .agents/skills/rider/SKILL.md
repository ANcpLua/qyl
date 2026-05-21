---
name: rider-mcp-server
description: >
    AI-parsable mapping of Rider MCP Server tool calls from the JetBrains MCP
    documentation ("Last modified: 22 April 2026"), plus Extended MCP Server
    plugin additions. Use this for deterministic tool dispatch, parameter checks,
    and prompt-time validation.
compatibility: >
    Local usage only. This mapping is derived from the published tool list and
    manual probing of the running Rider MCP server.
disable-model-invocation: true
user-invocable: false
---

# Rider MCP Server — Tool Map (AI Parsable)

Source context:
- External client setup docs for Rider MCP
- Manual runtime probe from `127.0.0.1:64342/stream` and `.../sse`
- Extended MCP Server plugin docs (JetBrains Marketplace + tool-level descriptions)

```json
{
    "server": "rider-mcp",
    "version_hint": "2026.2+",
    "extension_reference_skill": "/Users/ancplua/.codex/skills/rider-mcp-server/SKILL.md",
    "extension_reference_intent": "authoritative parsable registry for Extended MCP Server plugin tool behavior",
    "extensions": [
        {
            "id": "com.jetbrains.extended-mcp-server",
            "name": "Extended MCP Server",
            "type": "jetbrains-plugin",
            "summary": "Extends JetBrains MCP with dependency-aware search/read and dependency sync.",
            "plugin_url": "https://plugins.jetbrains.com/plugin/29460-extended-mcp-server/",
            "docs_url": "https://www.jetbrains.com/help/idea/mcp-server.html#external-client-setup",
            "features": [
                "search_in_dependencies_by_regex",
                "get_dependency_file_text",
                "sync_project"
            ]
        }
    ],
    "tool_categories": [
        "analysis",
        "code_insight",
        "database",
        "debugger",
        "devkit",
        "execution",
        "file",
        "formatting",
        "inspection",
        "monorepo",
        "read",
        "refactoring",
        "notebook",
        "search",
        "terminal",
        "text",
        "vcs",
        "extension"
    ],
    "tools": [
        {
            "name": "build_project",
            "aliases": ["build_solution"],
            "category": "analysis",
            "required_params": ["projectPath"],
            "optional_params": ["rebuild", "filesToRebuild", "timeout"],
            "notes": "Builds project/files and returns compiler diagnostics."
        },
        {
            "name": "get_file_problems",
            "category": "analysis",
            "required_params": ["projectPath", "filePath"],
            "optional_params": ["errorsOnly", "timeout"],
            "notes": "Runs IDE inspections on a single file."
        },
        {
            "name": "get_project_dependencies",
            "category": "analysis",
            "required_params": ["projectPath"],
            "optional_params": [],
            "notes": "List project dependencies."
        },
        {
            "name": "get_project_modules",
            "category": "analysis",
            "required_params": ["projectPath"],
            "optional_params": [],
            "notes": "List modules and module metadata."
        },
        {
            "name": "get_symbol_info",
            "category": "code_insight",
            "required_params": ["projectPath", "filePath", "line", "column"],
            "optional_params": [],
            "notes": "Quick Documentation-equivalent symbol lookup."
        },
        {
            "name": "get_database_object_description",
            "category": "database",
            "required_params": ["connectionId", "databaseName", "schemaName", "kind", "objectName", "projectPath"],
            "optional_params": [],
            "notes": "Read-only schema object description."
        },
        {
            "name": "list_database_connections",
            "category": "database",
            "required_params": ["projectPath"],
            "optional_params": [],
            "notes": "List database connections in project."
        },
        {
            "name": "test_database_connection",
            "category": "database",
            "required_params": ["projectPath", "id"],
            "optional_params": [],
            "notes": "Ping a configured connection and return diagnostics."
        },
        {
            "name": "list_database_schemas",
            "category": "database",
            "required_params": ["projectPath", "connectionId", "selectedOnly"],
            "optional_params": [],
            "notes": "List schemas for a database connection."
        },
        {
            "name": "list_schema_object_kinds",
            "category": "database",
            "required_params": ["projectPath", "connectionId"],
            "optional_params": [],
            "notes": "List supported object kinds (table/view/etc.)."
        },
        {
            "name": "list_schema_objects",
            "category": "database",
            "required_params": ["projectPath", "connectionId", "schemaName", "databaseName", "kind"],
            "optional_params": [],
            "notes": "List schema objects for a selected connection and schema."
        },
        {
            "name": "list_recent_sql_queries",
            "category": "database",
            "required_params": ["projectPath", "connectionId"],
            "optional_params": [],
            "notes": "Premium-only tool; returns active/recent query sessions."
        },
        {
            "name": "cancel_sql_query",
            "category": "database",
            "required_params": ["projectPath", "sessionId"],
            "optional_params": [],
            "notes": "Cancel a running SQL session."
        },
        {
            "name": "execute_sql_query",
            "category": "database",
            "required_params": ["projectPath", "connectionId", "queryText"],
            "optional_params": [],
            "notes": "Execute SQL and return CSV output when rows are returned."
        },
        {
            "name": "preview_table_data",
            "category": "database",
            "required_params": ["projectPath", "connectionId", "schemaName", "databaseName", "tableName"],
            "optional_params": ["maxRowCount"],
            "notes": "Preview table/view-like object content as CSV."
        },
        {
            "name": "xdebug_control_session",
            "category": "debugger",
            "required_params": ["projectPath", "sessionId", "action"],
            "optional_params": ["timeout", "eventsLimit", "clearEventsAfterRead"],
            "notes": "Controls running debug session (step, resume, pause, stop, wait, drain)."
        },
        {
            "name": "xdebug_evaluate_expression",
            "category": "debugger",
            "required_params": ["projectPath", "expression"],
            "optional_params": ["sessionId", "frameIndex", "depth"],
            "notes": "Evaluate expression in current frame of suspended session."
        },
        {
            "name": "xdebug_get_debugger_status",
            "category": "debugger",
            "required_params": ["projectPath"],
            "optional_params": [],
            "notes": "List all active debug sessions and activeSessionId."
        },
        {
            "name": "xdebug_get_frame_values",
            "category": "debugger",
            "required_params": ["projectPath"],
            "optional_params": ["sessionId", "frameIndex", "depth"],
            "notes": "Read local values in selected stack frame."
        },
        {
            "name": "xdebug_get_stack",
            "category": "debugger",
            "required_params": ["projectPath"],
            "optional_params": ["sessionId", "threadId", "limit", "offset"],
            "notes": "Get full call stack for a thread in session."
        },
        {
            "name": "xdebug_get_threads",
            "category": "debugger",
            "required_params": ["projectPath"],
            "optional_params": ["sessionId", "limit", "offset"],
            "notes": "List threads in the current debug session."
        },
        {
            "name": "xdebug_get_value_by_path",
            "category": "debugger",
            "required_params": ["projectPath", "path"],
            "optional_params": ["sessionId", "frameIndex", "depth"],
            "notes": "Drill into nested value by path in frame tree."
        },
        {
            "name": "xdebug_list_breakpoints",
            "category": "debugger",
            "required_params": ["projectPath"],
            "optional_params": ["filePath"],
            "notes": "List breakpoints globally or for a file."
        },
        {
            "name": "xdebug_remove_breakpoint",
            "category": "debugger",
            "required_params": ["projectPath"],
            "optional_params": ["breakpointId", "filePath", "line", "owner"],
            "notes": "Remove breakpoint(s) with owner/file/line selectors."
        },
        {
            "name": "xdebug_run_to_line",
            "category": "debugger",
            "required_params": ["projectPath", "filePath", "line"],
            "optional_params": ["sessionId", "timeout"],
            "notes": "Resume execution to target file:line in suspended session."
        },
        {
            "name": "xdebug_set_breakpoint",
            "category": "debugger",
            "required_params": ["projectPath"],
            "optional_params": ["breakpointId", "filePath", "line", "condition", "isLogMessage", "isLogStack", "temporary", "suspendPolicy", "enabled"],
            "notes": "Create/update/delete breakpoint by location or breakpointId."
        },
        {
            "name": "xdebug_set_variable",
            "category": "debugger",
            "required_params": ["projectPath", "path", "newValue"],
            "optional_params": ["sessionId", "frameIndex"],
            "notes": "Mutate expression target in the current suspended frame."
        },
        {
            "name": "xdebug_start_debugger_session",
            "category": "debugger",
            "required_params": ["projectPath"],
            "optional_params": ["configurationName", "filePath", "line", "timeout", "graceWaitMs", "programArguments", "workingDirectory", "envs"],
            "notes": "Start debug by run config or by file/line."
        },
        {
            "name": "find_lock_requirement_usages",
            "category": "devkit",
            "required_params": ["projectPath", "filePath", "line", "column"],
            "optional_params": ["timeout"],
            "notes": "Heuristic lock analysis around a symbol under cursor."
        },
        {
            "name": "find_threading_requirements_usages",
            "category": "devkit",
            "required_params": ["projectPath", "filePath", "line", "column"],
            "optional_params": ["timeout"],
            "notes": "Heuristic threading constraint analysis for method under cursor."
        },
        {
            "name": "execute_run_configuration",
            "category": "execution",
            "required_params": ["projectPath"],
            "optional_params": ["configurationName", "filePath", "line", "timeout", "waitForExit", "programArguments", "workingDirectory", "envs"],
            "notes": "Run config or code-location target and optionally wait for exit."
        },
        {
            "name": "get_run_configurations",
            "category": "execution",
            "required_params": ["projectPath"],
            "optional_params": ["filePath"],
            "notes": "Get project run configs or runnable points in file."
        },
        {
            "name": "create_new_file",
            "category": "file",
            "required_params": ["projectPath", "pathInProject"],
            "optional_params": ["text", "overwrite"],
            "notes": "Create file and optionally initialize content."
        },
        {
            "name": "find_files_by_glob",
            "category": "file",
            "required_params": ["projectPath", "globPattern"],
            "optional_params": ["subDirectoryRelativePath", "addExcluded", "fileCountLimit", "timeout"],
            "notes": "Recursive glob search in project."
        },
        {
            "name": "find_files_by_name_keyword",
            "category": "file",
            "required_params": ["projectPath"],
            "optional_params": [],
            "notes": "Signature omitted in pasted snippet; name suggests string keyword search by filename."
        },
        {
            "name": "get_all_open_file_paths",
            "category": "file",
            "required_params": ["projectPath"],
            "optional_params": [],
            "notes": "List current open editor files."
        },
        {
            "name": "list_directory_tree",
            "category": "file",
            "required_params": ["projectPath", "directoryPath"],
            "optional_params": ["maxDepth", "timeout"],
            "notes": "Project directory tree snapshot."
        },
        {
            "name": "open_file_in_editor",
            "category": "file",
            "required_params": ["projectPath", "filePath"],
            "optional_params": [],
            "notes": "Open file path in IDE editor."
        },
        {
            "name": "reformat_file",
            "category": "formatting",
            "required_params": ["projectPath", "path"],
            "optional_params": [],
            "notes": "Format a file via IDE formatter."
        },
        {
            "name": "validate_inspection_kts",
            "category": "inspection",
            "required_params": ["projectPath", "inspectionKtsCode", "pathToSpecification"],
            "optional_params": [],
            "notes": "Compile and validate inspection.kts against examples."
        },
        {
            "name": "generate_inspection_kts_api",
            "category": "inspection",
            "required_params": ["projectPath", "language"],
            "optional_params": ["wrapInTags"],
            "notes": "Return Inspection KTS API docs."
        },
        {
            "name": "generate_inspection_kts_examples",
            "category": "inspection",
            "required_params": ["projectPath", "language"],
            "optional_params": ["includeAdditionalExamples"],
            "notes": "Return inspection.kts templates and examples."
        },
        {
            "name": "generate_psi_tree",
            "category": "inspection",
            "required_params": ["projectPath", "code", "language"],
            "optional_params": [],
            "notes": "Generate PSI tree text for Java/Kotlin snippets."
        },
        {
            "name": "run_inspection_kts",
            "category": "inspection",
            "required_params": ["projectPath", "inspectionKtsCode"],
            "optional_params": ["contextPath", "targetFileContent"],
            "notes": "Compile inspection and run against target file content/path."
        },
        {
            "name": "get_project_status",
            "category": "monorepo",
            "required_params": ["projectPath"],
            "optional_params": [],
            "notes": "Indexing/scan readiness check before heavy analysis."
        },
        {
            "name": "read_file",
            "category": "read",
            "required_params": ["projectPath", "file_path"],
            "optional_params": ["mode", "start_line", "max_lines", "end_line", "start_column", "end_column", "start_offset", "end_offset", "context_lines", "max_levels", "include_siblings", "include_header"],
            "notes": "Read source with flexible slice/offset/columns/introspection modes."
        },
        {
            "name": "rename_refactoring",
            "category": "refactoring",
            "required_params": ["projectPath", "pathInProject", "symbolName", "newName"],
            "optional_params": [],
            "notes": "Safe symbol rename refactor across usages."
        },
        {
            "name": "runNotebookCell",
            "category": "notebook",
            "required_params": ["projectPath", "file_path"],
            "optional_params": ["cell_id"],
            "notes": "Execute one or all notebook cells."
        },
        {
            "name": "search_file",
            "category": "search",
            "required_params": ["projectPath", "q"],
            "optional_params": ["paths", "includeExcluded", "limit"],
            "notes": "Search files by glob path pattern."
        },
        {
            "name": "search_regex",
            "category": "search",
            "required_params": ["projectPath", "q"],
            "optional_params": ["paths", "limit", "projectPath"],
            "notes": "Regex search across project files."
        },
        {
            "name": "search_symbol",
            "category": "search",
            "required_params": ["projectPath", "q"],
            "optional_params": ["paths", "include_external", "limit"],
            "notes": "Search symbols by identifier fragments."
        },
        {
            "name": "search_text",
            "category": "search",
            "required_params": ["projectPath", "q"],
            "optional_params": ["paths", "limit"],
            "notes": "Plaintext search with snippet results."
        },
        {
            "name": "search_in_dependencies_by_regex",
            "category": "extension",
            "required_params": ["regexPattern"],
            "optional_params": ["fileMask", "caseSensitive", "maxUsageCount", "timeout", "projectPath"],
            "notes": "Dependency file regex search using IntelliJ's search engine."
        },
        {
            "name": "execute_terminal_command",
            "category": "terminal",
            "required_params": ["projectPath", "command"],
            "optional_params": ["executeInShell", "reuseExistingTerminalWindow", "timeout", "maxLinesCount", "truncateMode"],
            "notes": "Run shell command in IDE terminal (confirmation required unless brave mode)."
        },
        {
            "name": "get_file_text_by_path",
            "category": "text",
            "required_params": ["projectPath", "pathInProject"],
            "optional_params": ["truncateMode", "maxLinesCount"],
            "notes": "Read full file text by path relative to project root."
        },
        {
            "name": "replace_text_in_file",
            "category": "text",
            "required_params": ["projectPath", "pathInProject", "oldText", "newText"],
            "optional_params": ["replaceAll", "caseSensitive"],
            "notes": "Targeted in-file text replacement."
        },
        {
            "name": "search_in_files_by_regex",
            "category": "text",
            "required_params": ["projectPath", "regexPattern"],
            "optional_params": ["directoryToSearch", "fileMask", "caseSensitive", "maxUsageCount", "timeout"],
            "notes": "Regex text search via IntelliJ search engine."
        },
        {
            "name": "search_in_files_by_text",
            "category": "text",
            "required_params": ["projectPath", "searchText"],
            "optional_params": ["directoryToSearch", "fileMask", "caseSensitive", "maxUsageCount", "timeout"],
            "notes": "Text search via IntelliJ search engine."
        },
        {
            "name": "get_dependency_file_text",
            "category": "extension",
            "required_params": ["url", "lineNumber"],
            "optional_params": ["linesBefore", "linesAfter", "projectPath"],
            "notes": "Read text of a dependency file using the URL returned by dependency search."
        },
        {
            "name": "sync_project",
            "category": "extension",
            "required_params": [],
            "optional_params": ["projectPath"],
            "notes": "Synchronize external systems (Gradle, Maven, NPM, etc.) for project dependencies."
        },
        {
            "name": "get_repositories",
            "category": "vcs",
            "required_params": ["projectPath"],
            "optional_params": [],
            "notes": "List VCS roots for the project."
        }
    ],
    "routing_rules": [
        "Always include projectPath when known; many tools rely on it for disambiguation.",
        "If an extension tool is called, route it through the Extended MCP Server feature set when available.",
        "Debugger tools should avoid reusing frame/locals paths after resume/step operations.",
        "Many runtime results are session/scoped; pass current IDs from status/list calls.",
        "Some tool names in runtime may differ slightly from docs; treat `build_solution` as `build_project` alias."
    ]
}
```

## Runtime/Docs drift note

The pasted docs show `build_project`, but the running server in this environment has historically exposed both IDE-oriented names and older aliases. Use alias rules at dispatch time and keep both accepted as equivalent when present.

The `extensions` section now captures the Extended MCP Server plugin reference so
users can parse dependency tools and behavior from a single skill artifact.

## Reference artifact

- Extension registry skill: /Users/ancplua/.codex/skills/rider-mcp-server/SKILL.md
