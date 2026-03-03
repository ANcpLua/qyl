# CLAUDE.md

The role of this file is to describe common mistakes and confusion points that agents might encounter as they work in
this project. If you ever encounter something in the project that surprises you, please alert the developer working with
you and indicate that this is the case in the AgentMD file to help prevent future agents from having the same issue.

## GitHub Integration

<if-github-work scope="PRs, issues, repos, Actions, releases, code search, notifications">

**Rule: Always use `gh` CLI for GitHub operations. No MCP tools, no manual WebFetch to api.github.com, no scraping —
just `gh`.**

The `gh` CLI is authenticated, fast, and covers the entire GitHub API. Know the commands — don't blindly run `gh --help`
each time.

### Quick reference

| Task            | Command                                                                                         |
|-----------------|-------------------------------------------------------------------------------------------------|
| **PRs**         | `gh pr create`, `gh pr list`, `gh pr view`, `gh pr checkout 123`, `gh pr merge`, `gh pr checks` |
| **Issues**      | `gh issue create`, `gh issue list`, `gh issue view`                                             |
| **CI/Actions**  | `gh run list`, `gh run view`, `gh run watch`, `gh run rerun <id>`                               |
| **Code search** | `gh search code "query"`, `gh search repos "query"`                                             |
| **Releases**    | `gh release create`, `gh release list`                                                          |
| **Raw API**     | `gh api repos/{owner}/{repo}/...` (any REST/GraphQL endpoint, auth handled)                     |
| **Misc**        | `gh browse`, `gh status`, `gh repo clone`, `gh secret set`                                      |

### Anti-patterns

- Do NOT use WebFetch to hit `https://api.github.com/...` — use `gh api` instead.
- Do NOT use MCP GitHub tools when `gh` can do it.
- Do NOT run `gh --help` to rediscover commands you should already know from this table.

</if-github-work>

## Tool Preferences

<tool-routing>

**Rule: Prefer semantic/IDE tools over shell commands whenever possible.**

- Use `mcp__rider__*` as the default for code search, edits, refactoring, build, and diagnostics.
- Use `mcp__playwright__*` for all UI testing/verification.
- Use shell only when no Rider/Playwright tool covers the task.

### Rider MCP — code operations

| Intent               | Tool                                                               |
|----------------------|--------------------------------------------------------------------|
| Find file by name    | `mcp__rider__find_files_by_name_keyword`                           |
| Find file by glob    | `mcp__rider__search_file`                                          |
| Search text          | `mcp__rider__search_text`                                          |
| Search regex         | `mcp__rider__search_regex`                                         |
| Search symbol        | `mcp__rider__search_symbol`                                        |
| Read file (windowed) | `mcp__rider__read_file`                                            |
| Read file (full)     | `mcp__rider__get_file_text_by_path`                                |
| List directory tree  | `mcp__rider__list_directory_tree`                                  |
| Targeted edit        | `mcp__rider__replace_text_in_file`                                 |
| Create file          | `mcp__rider__create_new_file`                                      |
| Semantic rename      | `mcp__rider__rename_refactoring`                                   |
| Format file          | `mcp__rider__reformat_file`                                        |
| Symbol docs          | `mcp__rider__get_symbol_info`                                      |
| Build/verify         | `mcp__rider__build_project`                                        |
| Inspect file         | `mcp__rider__get_file_problems`                                    |
| Run configurations   | `mcp__rider__get_run_configurations` / `execute_run_configuration` |
| Shell fallback       | `mcp__rider__execute_terminal_command`                             |

### Playwright MCP — UI operations

| Intent           | Tool                                        |
|------------------|---------------------------------------------|
| Navigate         | `mcp__playwright__browser_navigate`         |
| Inspect state    | `mcp__playwright__browser_snapshot`         |
| Click            | `mcp__playwright__browser_click`            |
| Type             | `mcp__playwright__browser_type`             |
| Fill form        | `mcp__playwright__browser_fill_form`        |
| Select option    | `mcp__playwright__browser_select_option`    |
| Wait for state   | `mcp__playwright__browser_wait_for`         |
| Screenshot       | `mcp__playwright__browser_take_screenshot`  |
| Console errors   | `mcp__playwright__browser_console_messages` |
| Network requests | `mcp__playwright__browser_network_requests` |
| Advanced flow    | `mcp__playwright__browser_run_code`         |
| Tabs             | `mcp__playwright__browser_tabs`             |
| Close            | `mcp__playwright__browser_close`            |

### Default workflows

<after-code-change>

1. Apply targeted/semantic edit with Rider MCP.
2. Run `mcp__rider__build_project`.
3. Run `mcp__rider__get_file_problems` for touched files.

</after-code-change>

<ui-verification>

1. Navigate with Playwright.
2. Snapshot before each interaction.
3. Perform actions (click/type/fill/select).
4. Wait using text/textGone (avoid fixed sleeps).
5. Re-snapshot to assert state.
6. Check console errors (level=error).
7. Check network requests (includeStatic=false).
8. Take screenshot when visual proof is needed.

</ui-verification>

<investigate-symbol>

1. Use `search_symbol`.
2. Use `get_symbol_info`.
3. Use `search_text` for usages.

</investigate-symbol>

### Decision heuristics

- Semantic search (`search_symbol`) before regex when intent is code-structure aware.
- Targeted replace/refactor before whole-file rewrites.
- Always re-snapshot after navigation or major DOM changes.
- For UI checks, console + network validation is mandatory.
- Use shell only when no Rider/Playwright tool covers the task.

</tool-routing>

## Docker

<if-docker-work scope="build, run, compose, images, cleanup, deploy">

**Rule: Know the commands — don't guess flags or invent subcommands.**

### Quick reference

| Task                   | Command                            |
|------------------------|------------------------------------|
| **Build image**        | `docker build -t name:tag .`       |
| **Run disposable**     | `docker run --rm <image>`          |
| **Run detached**       | `docker run -d --name <n> <image>` |
| **List running**       | `docker ps`                        |
| **List all**           | `docker ps -a`                     |
| **Stop container**     | `docker stop <container>`          |
| **Stop all**           | `docker stop $(docker ps -q)`      |
| **Remove container**   | `docker rm <container>`            |
| **Remove all stopped** | `docker rm $(docker ps -aq)`       |
| **View logs**          | `docker logs <container>`          |
| **Follow logs**        | `docker logs -f <container>`       |
| **List images**        | `docker images`                    |
| **Remove image**       | `docker rmi <image>`               |
| **Disk usage**         | `docker system df`                 |
| **Clean everything**   | `docker system prune -a --volumes` |
| **Compose up**         | `docker compose up -d`             |
| **Compose down**       | `docker compose down`              |
| **Compose logs**       | `docker compose logs -f <service>` |

### Anti-patterns

- `docker --rm` does NOT exist — `--rm` is a flag for `docker run`.
- `docker prune all` does NOT exist — use `docker system prune -a`.
- Do NOT guess Docker subcommands. If unsure, check the table above.

</if-docker-work>

## C# Code Style

- C# 14 with preview features enabled
- File-scoped namespaces, primary constructors, required init properties
- Pattern matching, switch cases instead of if else functional style
- Roslyn generators: always IIncrementalGenerator, ForAttributeWithMetadataName, value-equatable models, raw strings
  over SyntaxFactory, test with ANcpLua.Roslyn.Utilities test infrastructure
- Never: ISourceGenerator, SyntaxFactory.NormalizeWhitespace(), store ISymbol in models, runtime reflection,
  dynamic/ExpandoObject, blocking async (.Result/.Wait()), any analyzer besides ANcpLua.Analyzers, suppressing analyzers
  that are fixable, suppressing null "!" eventho the code can be rewritten.

## A Note To The Agent

We are building this together. When you learn something non-obvious, add it here so future changes go faster.
