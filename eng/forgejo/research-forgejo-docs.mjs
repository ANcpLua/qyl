#!/usr/bin/env node
import { createHash } from 'node:crypto';
import { createWriteStream, mkdirSync, readdirSync, readFileSync, statSync, writeFileSync } from 'node:fs';
import { get } from 'node:https';
import { dirname, extname, join, relative } from 'node:path';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

const gitTimeoutMs = 120_000;
const scriptDir = dirname(fileURLToPath(import.meta.url));
const workspaceRoot = resolveWorkspaceRoot(scriptDir);
const outputRoot = join(workspaceRoot, 'artifacts', 'forgejo-research');
const repoRoot = join(outputRoot, 'repos');
const sourceMetadataRoot = join(outputRoot, 'source-metadata');
const swaggerPath = join(sourceMetadataRoot, 'forgejo-v15-swagger.json');
const noRefresh = process.argv.includes('--no-refresh');

// `code.forgejo.org/forgejo/forgejo` is the canonical Forgejo source; the
// `codeberg.org/forgejo/forgejo` mirror has identical content and is omitted to
// avoid duplicating ~all server source files in the corpus.
const upstreamRepos = [
  {
    name: 'qyl-v15',
    cloneUrl: 'https://v15.next.forgejo.org/ANcpLua/qyl.git'
  },
  {
    name: 'forgejo-docs',
    cloneUrl: 'https://codeberg.org/forgejo/docs.git'
  },
  {
    name: 'forgejo-website',
    cloneUrl: 'https://codeberg.org/forgejo/website.git'
  },
  {
    name: 'forgejo-runner',
    cloneUrl: 'https://code.forgejo.org/forgejo/runner.git'
  },
  {
    name: 'forgejo',
    cloneUrl: 'https://code.forgejo.org/forgejo/forgejo.git'
  }
];

const corpusSources = [
  {
    name: 'qyl-workspace',
    root: workspaceRoot,
    local: true,
    trackedOnly: true
  },
  ...upstreamRepos.map((repo) => ({
    name: repo.name,
    root: join(repoRoot, repo.name),
    local: false
  }))
];

const skippedDirectories = new Set([
  '.git',
  '.cache',
  '.next',
  '.nuxt',
  '.pnpm-store',
  '.turbo',
  '.venv',
  'artifacts',
  'bin',
  'build',
  'coverage',
  'dist',
  'node_modules',
  'obj',
  'target',
  'tmp',
  'vendor'
]);

const includedExtensions = new Set([
  '.adoc',
  '.astro',
  '.cs',
  '.go',
  '.json',
  '.md',
  '.mdx',
  '.ps1',
  '.rst',
  '.sh',
  '.toml',
  '.ts',
  '.tsx',
  '.txt',
  '.yaml',
  '.yml'
]);

const maxBytes = 1_000_000;
const maxSourceBytes = 50_000_000;
const requestTimeoutMs = 30_000;
const maxRedirects = 5;

// Load shared credential patterns from the same directory as this script
const credentialPatterns = JSON.parse(
  readFileSync(join(scriptDir, 'credential-patterns.json'), 'utf8')
);
const credentialRedactionRules = credentialPatterns.map((rule) => {
  const flags = rule.flags || 'gi';
  return [new RegExp(rule.pattern, flags), rule.replacement];
});

const searchTerms = [
  'actions/runners',
  'registration token',
  'runner registration',
  'runner:',
  'labels:',
  'runs-on',
  'workflow_dispatch',
  '.forgejo/workflows',
  'summary',
  'summarize',
  'trace summary',
  'error summary',
  'session summary'
];

mkdirSync(repoRoot, { recursive: true });
mkdirSync(sourceMetadataRoot, { recursive: true });

for (const repo of upstreamRepos) {
  refreshRepo(repo);
}

if (!noRefresh || !exists(swaggerPath)) {
  await download(
    'https://v15.next.forgejo.org/swagger.v1.json',
    swaggerPath
  );
}

const manifest = [];
const hits = [];
const hitsPerTermCap = 6;
const hitCounts = new Map(searchTerms.map((term) => [term, 0]));
const lowerTerms = searchTerms.map((term) => [term, term.toLowerCase()]);
let totalFiles = 0;
let totalBytes = 0;
const corpusPath = join(outputRoot, 'corpus.ndjson');
const corpusStream = createWriteStream(corpusPath, { encoding: 'utf8' });
for (const source of corpusSources) {
  const files = collectFiles(source);
  let sourceBytes = 0;
  for (const file of files) {
    const content = readFileSync(file, 'utf8');
    const bytes = Buffer.byteLength(content);
    sourceBytes += bytes;
    if (sourceBytes > maxSourceBytes) {
      throw new Error(`${source.name} exceeded ${maxSourceBytes} byte corpus limit`);
    }

    const redactedContent = redactCredentialText(content);
    const digest = sha256(content);
    const path = relative(source.root, file);
    corpusStream.write(JSON.stringify({
      source: source.name,
      path,
      bytes,
      sha256: digest,
      content: redactedContent
    }) + '\n');

    // Match search terms against the redacted body during the walk so we don't
    // need to retain the full corpus in memory after the loop. Per-term cap is
    // enforced via hitCounts; once a term hits the cap we skip it for the rest
    // of the run.
    const lowerContent = redactedContent.toLowerCase();
    for (const [term, lowerTerm] of lowerTerms) {
      if (hitCounts.get(term) >= hitsPerTermCap) {
        continue;
      }
      const index = lowerContent.indexOf(lowerTerm);
      if (index < 0) {
        continue;
      }
      hits.push({
        term,
        source: source.name,
        path,
        excerpt: excerpt(redactedContent, index)
      });
      hitCounts.set(term, hitCounts.get(term) + 1);
    }
  }

  manifest.push({
    source: source.name,
    root: source.local ? '.' : relative(workspaceRoot, source.root),
    commit: source.local ? currentCommit(workspaceRoot) : currentCommit(source.root),
    dirty: source.local ? isDirty(workspaceRoot) : isDirty(source.root),
    files: files.length,
    bytes: sourceBytes,
    redacted: true,
    truncated: false
  });
  totalFiles += files.length;
  totalBytes += sourceBytes;
}

await new Promise((resolve, reject) => corpusStream.end(resolve).on('error', reject));
const routeIndex = buildRouteIndex(swaggerPath);
writeFileSync(join(outputRoot, 'manifest.json'), JSON.stringify(manifest, null, 2) + '\n');
writeFileSync(join(outputRoot, 'route-index.json'), JSON.stringify(routeIndex, null, 2) + '\n');
writeFileSync(join(outputRoot, 'summary-api-notes.md'), renderNotes(manifest, routeIndex, hits.slice(0, 80)));

console.log(`Forgejo research corpus written to ${relative(workspaceRoot, outputRoot)}`);
console.log(`Sources: ${manifest.length}, files: ${totalFiles}, bytes: ${totalBytes}`);
console.log(`Route index entries: ${routeIndex.length}`);

function refreshRepo(repo) {
  const checkoutPath = join(repoRoot, repo.name);
  const gitPath = join(checkoutPath, '.git');

  if (!exists(gitPath)) {
    runGit(['clone', '--depth', '1', repo.cloneUrl, checkoutPath]);
    return;
  }

  if (!noRefresh) {
    runGit(['-C', checkoutPath, 'pull', '--ff-only']);
  }
}

function collectFiles(source) {
  if (source.trackedOnly) {
    return collectTrackedFiles(source.root);
  }

  const files = [];
  walk(source.root, files);
  files.sort();
  return files;
}

function collectTrackedFiles(root) {
  const result = runGit(['-C', root, 'ls-files', '-z']);
  const files = result.stdout
    .split('\0')
    .filter(Boolean)
    .map((path) => join(root, path))
    .filter(shouldIncludeFile);
  files.sort();
  return files;
}

function walk(current, files) {
  for (const entry of readdirSync(current, { withFileTypes: true })) {
    if (entry.isDirectory()) {
      if (!skippedDirectories.has(entry.name)) {
        walk(join(current, entry.name), files);
      }
      continue;
    }

    if (!entry.isFile()) {
      continue;
    }

    const file = join(current, entry.name);
    if (shouldIncludeFile(file)) {
      files.push(file);
    }
  }
}

function shouldIncludeFile(file) {
  const ext = extname(file).toLowerCase();
  if (!includedExtensions.has(ext)) {
    return false;
  }

  const size = statSync(file).size;
  if (size === 0 || size > maxBytes) {
    return false;
  }

  return !isProbablyBinary(file);
}

function buildRouteIndex(swaggerPath) {
  const swagger = JSON.parse(readFileSync(swaggerPath, 'utf8'));
  const paths = swagger.paths ?? {};
  const routeTerms = [
    'actions',
    'runners',
    'workflows',
    'repos',
    'pulls',
    'statuses',
    'secrets',
    'variables'
  ];

  return Object.entries(paths)
    .flatMap(([path, operations]) =>
      Object.entries(operations)
        .filter(([method]) => ['get', 'post', 'put', 'patch', 'delete'].includes(method))
        .map(([method, operation]) => ({
          method: method.toUpperCase(),
          path,
          operationId: operation.operationId ?? '',
          summary: operation.summary ?? ''
        })))
    .filter((route) => {
      const haystack = `${route.path} ${route.operationId} ${route.summary}`.toLowerCase();
      return routeTerms.some((term) => haystack.includes(term));
    })
    .sort((a, b) => `${a.path} ${a.method}`.localeCompare(`${b.path} ${b.method}`));
}

function renderNotes(manifest, routeIndex, hits) {
  const lines = [
    '# Forgejo Research Notes',
    '',
    'Generated from local shallow checkouts and live Forgejo v15 Swagger.',
    '',
    '## Sources',
    '',
    '| Source | Commit | Dirty | Files | Bytes |',
    '| --- | --- | --- | ---: | ---: |'
  ];

  for (const source of manifest) {
    lines.push(`| ${escapeCell(source.source)} | ${escapeCell(source.commit)} | ${source.dirty ? 'yes' : 'no'} | ${source.files} | ${source.bytes} |`);
  }

  lines.push(
    '',
    '## Forgejo API Surface',
    '',
    `Indexed ${routeIndex.length} Forgejo v15 API routes related to repositories, Actions, runners, workflows, statuses, secrets, and variables.`,
    '',
    '## High-Signal Search Hits',
    '',
    '| Term | Source | Path | Excerpt |',
    '| --- | --- | --- | --- |'
  );

  for (const hit of hits) {
    lines.push(`| ${escapeCell(hit.term)} | ${escapeCell(hit.source)} | ${escapeCell(hit.path)} | ${escapeCell(hit.excerpt)} |`);
  }

  lines.push('', '## qyl Summary API Surfaces', '');
  const summarySurfaces = [
    ['services/qyl.mcp/Tools/SummaryTools.cs', 'exposes the MCP tool boundary.'],
    ['services/qyl.mcp/Tools/SummaryFacade.cs', 'composes collector paths into raw context and optionally delegates to the configured LLM agent.'],
  ];
  const presentSurfaces = summarySurfaces.filter(([file]) => exists(join(workspaceRoot, file)));
  if (presentSurfaces.length === 0) {
    lines.push(
      'No qyl.mcp summary surfaces were found in this checkout.',
      ''
    );
  } else {
    for (const [file, description] of presentSurfaces) {
      lines.push(`- \`${file}\` ${description}`);
    }
    lines.push(
      '- Current two-path summaries are error issue + recent events and session + session spans. Trace summary is currently a single collector path over all spans for one trace.',
      ''
    );
  }

  return lines.join('\n');
}

// Search-hit collection happens inline during the file walk above (so the full
// corpus does not have to be retained in memory). `excerpt` is the only helper
// the inline matcher needs: `content` is already redacted, so this just slices
// a window around the match without re-running the regex pass.
function excerpt(content, index) {
  const start = Math.max(0, index - 90);
  const end = Math.min(content.length, index + 180);
  return content
    .slice(start, end)
    .replaceAll(/\s+/g, ' ')
    .trim();
}

function redactCredentialText(value) {
  return credentialRedactionRules.reduce(
    (redacted, [pattern, replacement]) => redacted.replaceAll(pattern, replacement),
    value
  );
}

function currentCommit(repoPath) {
  return runGit(['-C', repoPath, 'rev-parse', '--short', 'HEAD']).stdout.trim();
}

function isDirty(repoPath) {
  return runGit(['-C', repoPath, 'status', '--porcelain']).stdout.trim().length > 0;
}

function isProbablyBinary(file) {
  const sample = readFileSync(file, { encoding: null }).subarray(0, 8000);
  return sample.includes(0);
}

function download(url, destination, redirectsRemaining = maxRedirects) {
  return new Promise((resolve, reject) => {
    mkdirSync(dirname(destination), { recursive: true });
    const request = get(url, (response) => {
      if (response.statusCode >= 300 && response.statusCode < 400 && response.headers.location) {
        if (redirectsRemaining <= 0) {
          reject(new Error(`GET ${url} exceeded redirect limit`));
          response.resume();
          return;
        }

        response.resume();
        download(new URL(response.headers.location, url).toString(), destination, redirectsRemaining - 1)
          .then(resolve, reject);
        return;
      }

      if (response.statusCode !== 200) {
        reject(new Error(`GET ${url} failed with ${response.statusCode}`));
        response.resume();
        return;
      }

      const chunks = [];
      response.on('data', (chunk) => chunks.push(chunk));
      response.on('end', () => {
        writeFileSync(destination, Buffer.concat(chunks));
        resolve();
      });
    }).on('error', reject);

    request.setTimeout(requestTimeoutMs, () => {
      request.destroy(new Error(`GET ${url} timed out after ${requestTimeoutMs}ms`));
    });
  });
}

function resolveWorkspaceRoot(startPath) {
  const result = spawnSync('git', ['-C', startPath, 'rev-parse', '--show-toplevel'], {
    encoding: 'utf8',
    stdio: ['ignore', 'pipe', 'pipe'],
    env: { ...process.env, GIT_TERMINAL_PROMPT: '0' },
    timeout: gitTimeoutMs
  });

  if (result.error) {
    throw result.error;
  }

  if (result.status !== 0) {
    throw new Error(`Unable to resolve qyl repository root from ${startPath}\n${result.stderr || result.stdout}`);
  }

  return result.stdout.trim();
}

function runGit(args) {
  return run('git', args, {
    env: { ...process.env, GIT_TERMINAL_PROMPT: '0' },
    timeout: gitTimeoutMs
  });
}

function run(command, args, options = {}) {
  const result = spawnSync(command, args, {
    cwd: workspaceRoot,
    encoding: 'utf8',
    stdio: ['ignore', 'pipe', 'pipe'],
    ...options
  });

  if (result.error) {
    throw result.error;
  }

  if (result.status !== 0) {
    throw new Error(`${command} ${args.join(' ')} failed\n${result.stderr || result.stdout}`);
  }

  return result;
}

function exists(path) {
  try {
    statSync(path);
    return true;
  } catch {
    return false;
  }
}

function sha256(content) {
  return createHash('sha256').update(content).digest('hex');
}

function escapeCell(value) {
  return String(value).replaceAll('|', '\\|').replaceAll('\n', ' ');
}
