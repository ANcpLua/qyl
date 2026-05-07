# Forgejo AI Review

This is the Forgejo-native review lane for private repositories. It keeps the GitHub-native CodeRabbit and Claude workflows untouched and adds a self-hosted Forgejo Actions path that reviews the pull request diff from inside the runner, then posts one managed comment back to the Forgejo pull request.

The workflow is intentionally explicit. Configure the model or command you want to use; do not depend on vendor "latest" aliases.

## Required Forgejo setup

1. Enable Forgejo Actions on the instance and repository.
2. Register a runner that can satisfy the `ubuntu-latest` label, or change `.forgejo/workflows/ai-review.yml` to the label your runner exposes.
3. Ensure the workflow token can create and update issue comments on pull requests. If the automatic token cannot write comments on your Forgejo instance, create a repository-scoped token and store it as `FORGEJO_TOKEN`.

## Provider configuration

At least one provider must be configured or the workflow exits without posting a comment.

| Secret | Meaning |
|---|---|
| `OPENAI_API_KEY` | Enables direct OpenAI Responses API review when `QYL_AI_REVIEW_OPENAI_MODEL` is also set. |
| `QYL_AI_REVIEW_OPENAI_MODEL` | Explicit OpenAI model name. Use a Codex-class Responses API model your account has access to. |
| `QYL_AI_REVIEW_OPENAI_REASONING_EFFORT` | Optional OpenAI reasoning effort, for models that support it. |
| `ANTHROPIC_API_KEY` | Enables direct Anthropic Messages API review when `QYL_AI_REVIEW_ANTHROPIC_MODEL` is also set. |
| `QYL_AI_REVIEW_ANTHROPIC_MODEL` | Explicit Anthropic model name. |
| `QYL_AI_REVIEW_CODEX_COMMAND` | Optional shell command for a locally installed Codex CLI. The command receives the review prompt on stdin. |
| `QYL_AI_REVIEW_CLAUDE_COMMAND` | Optional shell command for a locally installed Claude CLI. The command receives the review prompt on stdin. |
| `QYL_AI_REVIEW_CODERABBIT_COMMAND` | Optional shell command for CodeRabbit CLI. A typical value is `coderabbit review --plain --base "$QYL_AI_REVIEW_BASE_REF"`. |

Command providers also receive these environment variables:

| Variable | Meaning |
|---|---|
| `QYL_AI_REVIEW_PROMPT_FILE` | Full prompt file, including PR metadata and diff. |
| `QYL_AI_REVIEW_DIFF_FILE` | Diff-only file. |
| `QYL_AI_REVIEW_PR_NUMBER` | Pull request number. |
| `QYL_AI_REVIEW_REPOSITORY` | Repository full name. |
| `QYL_AI_REVIEW_BASE_REF` | Pull request base branch. |
| `QYL_AI_REVIEW_HEAD_REF` | Pull request head branch. |
| `QYL_AI_REVIEW_BASE_SHA` | Pull request base commit. |
| `QYL_AI_REVIEW_HEAD_SHA` | Pull request head commit. |

## Tuning

| Variable | Default | Meaning |
|---|---:|---|
| `QYL_AI_REVIEW_DIFF_CONTEXT` | `80` | Unified diff context lines. |
| `QYL_AI_REVIEW_MAX_DIFF_BYTES` | `240000` | Diff byte cap sent to reviewers. |
| `QYL_AI_REVIEW_MAX_OUTPUT_BYTES` | `90000` | Output cap captured from command reviewers. |
| `QYL_AI_REVIEW_TIMEOUT_SECONDS` | `900` | Per-command timeout. |
| `QYL_AI_REVIEW_OPENAI_MAX_OUTPUT_TOKENS` | `4096` | OpenAI response cap. |
| `QYL_AI_REVIEW_ANTHROPIC_MAX_TOKENS` | `4096` | Anthropic response cap. |
| `QYL_AI_REVIEW_MAX_COMMENT_BYTES` | `60000` | Forgejo comment cap before truncation. |

The script updates its previous comment by marker instead of adding a new comment on every push.
