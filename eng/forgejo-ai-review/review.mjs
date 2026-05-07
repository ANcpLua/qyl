#!/usr/bin/env node

import { spawn } from "node:child_process";
import { existsSync } from "node:fs";
import { mkdtemp, readFile, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import process from "node:process";

const marker = "<!-- qyl-forgejo-ai-review -->";
const env = process.env;

function bool(name, defaultValue) {
  const value = env[name];
  if (value === undefined || value === "") {
    return defaultValue;
  }

  return ["1", "true", "yes", "on"].includes(value.toLowerCase());
}

function int(name, defaultValue) {
  const value = Number.parseInt(env[name] ?? "", 10);
  return Number.isFinite(value) && value > 0 ? value : defaultValue;
}

function truncateUtf8(text, maxBytes) {
  const bytes = Buffer.byteLength(text, "utf8");
  if (bytes <= maxBytes) {
    return { text, truncated: false, originalBytes: bytes };
  }

  const markerText = `\n\n[truncated after ${maxBytes} bytes; original size ${bytes} bytes]\n`;
  const markerBytes = Buffer.byteLength(markerText, "utf8");
  const slice = Buffer.from(text, "utf8").subarray(0, Math.max(0, maxBytes - markerBytes));
  return {
    text: slice.toString("utf8").replace(/\uFFFD+$/u, "") + markerText,
    truncated: true,
    originalBytes: bytes,
  };
}

async function runProcess(file, args, options = {}) {
  const {
    input,
    shell = false,
    timeoutMs = 900_000,
    maxOutputBytes = 90_000,
    extraEnv = {},
  } = options;

  return await new Promise((resolve) => {
    const child = spawn(file, args, {
      shell,
      env: { ...env, ...extraEnv },
      stdio: ["pipe", "pipe", "pipe"],
    });

    let stdout = "";
    let stderr = "";
    let timedOut = false;

    const timer = setTimeout(() => {
      timedOut = true;
      child.kill("SIGTERM");
      setTimeout(() => child.kill("SIGKILL"), 5_000).unref();
    }, timeoutMs);

    child.stdout.on("data", (chunk) => {
      if (Buffer.byteLength(stdout, "utf8") < maxOutputBytes) {
        stdout += chunk.toString("utf8");
      }
    });

    child.stderr.on("data", (chunk) => {
      if (Buffer.byteLength(stderr, "utf8") < maxOutputBytes) {
        stderr += chunk.toString("utf8");
      }
    });

    child.on("error", (error) => {
      clearTimeout(timer);
      resolve({ exitCode: 127, stdout, stderr: `${stderr}\n${error.message}`, timedOut });
    });

    child.on("close", (exitCode) => {
      clearTimeout(timer);
      resolve({ exitCode, stdout, stderr, timedOut });
    });

    if (input !== undefined) {
      child.stdin.end(input);
    } else {
      child.stdin.end();
    }
  });
}

async function readEvent() {
  const eventPath = env.FORGEJO_EVENT_PATH ?? env.GITEA_EVENT_PATH ?? env.GITHUB_EVENT_PATH;
  if (!eventPath || !existsSync(eventPath)) {
    return {};
  }

  return JSON.parse(await readFile(eventPath, "utf8"));
}

function resolveRepository(event) {
  const fullName =
    env.FORGEJO_REPOSITORY ??
    env.GITEA_REPOSITORY ??
    env.GITHUB_REPOSITORY ??
    event.repository?.full_name;

  if (!fullName || !fullName.includes("/")) {
    throw new Error("Repository full name was not available from the event or environment.");
  }

  const [owner, ...repoParts] = fullName.split("/");
  return {
    owner,
    repo: repoParts.join("/"),
    fullName,
    serverUrl:
      env.FORGEJO_SERVER_URL ??
      env.GITEA_SERVER_URL ??
      env.GITHUB_SERVER_URL ??
      event.repository?.html_url?.replace(`/${fullName}`, "") ??
      "",
  };
}

function resolvePullRequest(event) {
  const pr = event.pull_request;
  if (!pr) {
    return undefined;
  }

  return {
    number: pr.number ?? event.number,
    title: pr.title ?? "",
    author: pr.user?.login ?? pr.user?.username ?? "",
    htmlUrl: pr.html_url ?? pr.url ?? "",
    baseRef: pr.base?.ref ?? "",
    baseSha: pr.base?.sha ?? "",
    headRef: pr.head?.ref ?? "",
    headSha: pr.head?.sha ?? "",
  };
}

async function getDiff(pr) {
  if (env.QYL_AI_REVIEW_DIFF_FILE && existsSync(env.QYL_AI_REVIEW_DIFF_FILE)) {
    const diff = await readFile(env.QYL_AI_REVIEW_DIFF_FILE, "utf8");
    return truncateUtf8(diff, int("QYL_AI_REVIEW_MAX_DIFF_BYTES", 240_000));
  }

  const context = int("QYL_AI_REVIEW_DIFF_CONTEXT", 80);
  const maxBytes = int("QYL_AI_REVIEW_MAX_DIFF_BYTES", 240_000);
  const attempts = [];

  if (pr.baseSha && pr.headSha) {
    attempts.push(["diff", "--find-renames", "--diff-filter=ACMRT", `--unified=${context}`, `${pr.baseSha}...${pr.headSha}`]);
    attempts.push(["diff", "--find-renames", "--diff-filter=ACMRT", `--unified=${context}`, pr.baseSha, pr.headSha]);
  }

  if (pr.baseRef) {
    attempts.push(["diff", "--find-renames", "--diff-filter=ACMRT", `--unified=${context}`, `origin/${pr.baseRef}...HEAD`]);
  }

  attempts.push(["diff", "--find-renames", "--diff-filter=ACMRT", `--unified=${context}`, "HEAD~1", "HEAD"]);

  const errors = [];
  for (const args of attempts) {
    const result = await runProcess("git", args, { maxOutputBytes: maxBytes + 32_000 });
    if (result.exitCode === 0) {
      return truncateUtf8(result.stdout, maxBytes);
    }

    errors.push(`git ${args.join(" ")}\n${result.stderr.trim()}`);
  }

  throw new Error(`Unable to compute pull request diff.\n\n${errors.join("\n\n")}`);
}

function buildSystemPrompt() {
  return [
    "You are reviewing a pull request for qyl.",
    "Prioritize concrete correctness findings: bugs, behavioral regressions, security issues, concurrency defects, data loss, broken generated-artifact contracts, and CI/build risks.",
    "For qyl semantic-convention work, treat upstream OpenTelemetry YAML and Weaver-based generation as the source of truth.",
    "Ignore style nits unless they hide a real defect.",
    "Do not propose broad rewrites from a diff-only review.",
    "Return Markdown with sections: Findings, Open Questions, and Verification Notes.",
    "Each finding must name the affected file and the line from the diff when visible.",
    "If there are no blocking findings in the supplied diff, say that directly and keep the response short.",
  ].join("\n");
}

function buildPrompt(repository, pr, diffResult) {
  const truncation = diffResult.truncated
    ? `\n\nDiff was truncated from ${diffResult.originalBytes} bytes. Treat missing context as an explicit uncertainty.`
    : "";

  return `${buildSystemPrompt()}

Repository: ${repository.fullName}
Pull request: #${pr.number} ${pr.title}
Author: ${pr.author}
Base: ${pr.baseRef} ${pr.baseSha}
Head: ${pr.headRef} ${pr.headSha}${truncation}

Review this diff:

\`\`\`diff
${diffResult.text}
\`\`\`
`;
}

function extractOpenAIText(response) {
  if (typeof response.output_text === "string" && response.output_text.trim() !== "") {
    return response.output_text;
  }

  const pieces = [];
  for (const item of response.output ?? []) {
    for (const content of item.content ?? []) {
      if (typeof content.text === "string") {
        pieces.push(content.text);
      } else if (typeof content.output_text === "string") {
        pieces.push(content.output_text);
      }
    }
  }

  return pieces.join("\n").trim();
}

function extractAnthropicText(response) {
  return (response.content ?? [])
    .filter((item) => item.type === "text" && typeof item.text === "string")
    .map((item) => item.text)
    .join("\n")
    .trim();
}

async function postJson(url, headers, body) {
  const response = await fetch(url, {
    method: "POST",
    headers: {
      "content-type": "application/json",
      ...headers,
    },
    body: JSON.stringify(body),
  });

  const text = await response.text();
  if (!response.ok) {
    throw new Error(`${response.status} ${response.statusText}: ${text}`);
  }

  return text ? JSON.parse(text) : {};
}

async function runOpenAI(prompt) {
  const apiKey = env.OPENAI_API_KEY;
  const model = env.QYL_AI_REVIEW_OPENAI_MODEL;
  if (!apiKey || !model) {
    return undefined;
  }

  const body = {
    model,
    max_output_tokens: int("QYL_AI_REVIEW_OPENAI_MAX_OUTPUT_TOKENS", 4096),
    input: [
      {
        role: "system",
        content: [{ type: "input_text", text: buildSystemPrompt() }],
      },
      {
        role: "user",
        content: [{ type: "input_text", text: prompt }],
      },
    ],
  };

  if (env.QYL_AI_REVIEW_OPENAI_REASONING_EFFORT) {
    body.reasoning = { effort: env.QYL_AI_REVIEW_OPENAI_REASONING_EFFORT };
  }

  const endpoint = env.OPENAI_RESPONSES_URL ?? "https://api.openai.com/v1/responses";
  const response = await postJson(endpoint, { authorization: `Bearer ${apiKey}` }, body);
  const text = extractOpenAIText(response);
  return { name: `OpenAI (${model})`, text: text || "[OpenAI returned no review text.]" };
}

async function runAnthropic(prompt) {
  const apiKey = env.ANTHROPIC_API_KEY;
  const model = env.QYL_AI_REVIEW_ANTHROPIC_MODEL;
  if (!apiKey || !model) {
    return undefined;
  }

  const endpoint = env.ANTHROPIC_MESSAGES_URL ?? "https://api.anthropic.com/v1/messages";
  const response = await postJson(
    endpoint,
    {
      "x-api-key": apiKey,
      "anthropic-version": env.ANTHROPIC_VERSION ?? "2023-06-01",
    },
    {
      model,
      max_tokens: int("QYL_AI_REVIEW_ANTHROPIC_MAX_TOKENS", 4096),
      system: buildSystemPrompt(),
      messages: [{ role: "user", content: prompt }],
    },
  );

  const text = extractAnthropicText(response);
  return { name: `Anthropic (${model})`, text: text || "[Anthropic returned no review text.]" };
}

async function runCommandReviewer(name, command, prompt, promptFile, diffFile, repository, pr) {
  if (!command) {
    return undefined;
  }

  const timeoutMs = int("QYL_AI_REVIEW_TIMEOUT_SECONDS", 900) * 1000;
  const result = await runProcess(command, [], {
    shell: true,
    input: prompt,
    timeoutMs,
    maxOutputBytes: int("QYL_AI_REVIEW_MAX_OUTPUT_BYTES", 90_000),
    extraEnv: {
      QYL_AI_REVIEW_PROMPT_FILE: promptFile,
      QYL_AI_REVIEW_DIFF_FILE: diffFile,
      QYL_AI_REVIEW_PR_NUMBER: String(pr.number),
      QYL_AI_REVIEW_REPOSITORY: repository.fullName,
      QYL_AI_REVIEW_BASE_REF: pr.baseRef,
      QYL_AI_REVIEW_HEAD_REF: pr.headRef,
      QYL_AI_REVIEW_BASE_SHA: pr.baseSha,
      QYL_AI_REVIEW_HEAD_SHA: pr.headSha,
    },
  });

  const text = [result.stdout.trim(), result.stderr.trim() ? `\n\nstderr:\n${result.stderr.trim()}` : ""]
    .join("")
    .trim();

  if (result.exitCode !== 0 || result.timedOut) {
    return {
      name,
      failed: true,
      text:
        text ||
        `[${name} failed with exit code ${result.exitCode}${result.timedOut ? " after timeout" : ""}.]`,
    };
  }

  return { name, text: text || `[${name} completed without output.]` };
}

async function runReviewers(prompt, diff, repository, pr) {
  const temp = await mkdtemp(join(tmpdir(), "qyl-ai-review-"));
  const promptFile = join(temp, "prompt.md");
  const diffFile = join(temp, "diff.patch");
  await writeFile(promptFile, prompt, "utf8");
  await writeFile(diffFile, diff, "utf8");

  const reviewers = [
    { name: "OpenAI API", run: () => runOpenAI(prompt) },
    { name: "Anthropic API", run: () => runAnthropic(prompt) },
    {
      name: "Codex CLI",
      run: () => runCommandReviewer("Codex CLI", env.QYL_AI_REVIEW_CODEX_COMMAND, prompt, promptFile, diffFile, repository, pr),
    },
    {
      name: "Claude CLI",
      run: () => runCommandReviewer("Claude CLI", env.QYL_AI_REVIEW_CLAUDE_COMMAND, prompt, promptFile, diffFile, repository, pr),
    },
    {
      name: "CodeRabbit CLI",
      run: () => runCommandReviewer("CodeRabbit CLI", env.QYL_AI_REVIEW_CODERABBIT_COMMAND, prompt, promptFile, diffFile, repository, pr),
    },
  ];

  const results = [];
  for (const reviewer of reviewers) {
    try {
      const result = await reviewer.run();
      if (result) {
        results.push(result);
      }
    } catch (error) {
      results.push({
        name: reviewer.name,
        failed: true,
        text: error instanceof Error ? error.message : String(error),
      });
    }
  }

  return results;
}

function formatComment(repository, pr, results) {
  const failed = results.filter((result) => result.failed);
  const status = failed.length === 0 ? "completed" : `completed with ${failed.length} provider error(s)`;
  const sections = results.map((result) => {
    const title = result.failed ? `${result.name} failed` : result.name;
    return `<details open>
<summary>${title}</summary>

${result.text.trim()}

</details>`;
  });

  const body = `${marker}
## Forgejo AI Review

Status: ${status}

Repository: \`${repository.fullName}\`
Pull request: #${pr.number}
Head: \`${pr.headSha || pr.headRef || "unknown"}\`

${sections.join("\n\n")}
`;

  return truncateUtf8(body, int("QYL_AI_REVIEW_MAX_COMMENT_BYTES", 60_000)).text;
}

async function forgejoFetch(apiUrl, path, token, options = {}) {
  const response = await fetch(`${apiUrl}${path}`, {
    ...options,
    headers: {
      accept: "application/json",
      authorization: `token ${token}`,
      "content-type": "application/json",
      ...(options.headers ?? {}),
    },
  });

  const text = await response.text();
  if (!response.ok) {
    throw new Error(`${response.status} ${response.statusText}: ${text}`);
  }

  return text ? JSON.parse(text) : {};
}

async function postOrUpdateComment(repository, pr, body) {
  if (!bool("QYL_AI_REVIEW_POST_COMMENT", true)) {
    console.log(body);
    return;
  }

  const token = env.FORGEJO_TOKEN ?? env.GITEA_TOKEN ?? env.GITHUB_TOKEN;
  if (!token) {
    if (bool("QYL_AI_REVIEW_REQUIRE_COMMENT", true)) {
      throw new Error("No Forgejo token available for posting the review comment.");
    }

    console.log(body);
    return;
  }

  const serverUrl = repository.serverUrl.replace(/\/$/u, "");
  const apiUrl = (env.FORGEJO_API_URL ?? env.GITEA_API_URL ?? `${serverUrl}/api/v1`).replace(/\/$/u, "");
  const owner = encodeURIComponent(repository.owner);
  const repo = encodeURIComponent(repository.repo);
  const issueNumber = encodeURIComponent(String(pr.number));

  const comments = await forgejoFetch(apiUrl, `/repos/${owner}/${repo}/issues/${issueNumber}/comments`, token);
  const previous = Array.isArray(comments)
    ? comments.find((comment) => typeof comment.body === "string" && comment.body.includes(marker))
    : undefined;

  if (previous?.id) {
    await forgejoFetch(apiUrl, `/repos/${owner}/${repo}/issues/comments/${previous.id}`, token, {
      method: "PATCH",
      body: JSON.stringify({ body }),
    });
    console.log(`Updated Forgejo AI review comment ${previous.id}.`);
    return;
  }

  const created = await forgejoFetch(apiUrl, `/repos/${owner}/${repo}/issues/${issueNumber}/comments`, token, {
    method: "POST",
    body: JSON.stringify({ body }),
  });
  console.log(`Created Forgejo AI review comment ${created.id ?? "(unknown id)"}.`);
}

async function main() {
  const event = await readEvent();
  const repository = resolveRepository(event);
  const pr = resolvePullRequest(event);
  if (!pr?.number) {
    console.log("No pull request event detected; skipping Forgejo AI review.");
    return;
  }

  const diffResult = await getDiff(pr);
  if (diffResult.text.trim() === "") {
    console.log("Pull request diff is empty; skipping Forgejo AI review.");
    return;
  }

  const prompt = buildPrompt(repository, pr, diffResult);
  const results = await runReviewers(prompt, diffResult.text, repository, pr);
  if (results.length === 0) {
    console.log("No AI review provider configured; set at least one provider secret to enable reviews.");
    return;
  }

  const body = formatComment(repository, pr, results);
  await postOrUpdateComment(repository, pr, body);

  if (results.some((result) => result.failed) && bool("QYL_AI_REVIEW_FAIL_ON_PROVIDER_ERROR", false)) {
    throw new Error("At least one AI review provider failed.");
  }
}

main().catch((error) => {
  console.error(error instanceof Error ? error.stack ?? error.message : error);
  process.exitCode = 1;
});
