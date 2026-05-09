#!/usr/bin/env node
import { createHash } from 'node:crypto';
import { mkdirSync, readdirSync, readFileSync, statSync, writeFileSync } from 'node:fs';
import { get } from 'node:https';
import { dirname, extname, join, relative } from 'node:path';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

const workspaceRoot = process.cwd();
const outputRoot = join(workspaceRoot, 'artifacts', 'forgejo-research');
const repoRoot = join(outputRoot, 'repos');
const sourceMetadataRoot = join(outputRoot, 'source-metadata');
const noRefresh = process.argv.includes('--no-refresh');

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
    name: 'forgejo-server',
    cloneUrl: 'https://codeberg.org/forgejo/forgejo.git'
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
const requestTimeoutMs = 30_000;
const maxRedirects = 5;

// Load shared credential patterns from the same directory as this script
const scriptDir = dirname(fileURLToPath(import.meta.url));
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

await download(
  'https://v15.next.forgejo.org/swagger.v1.json',
  join(sourceMetadataRoot, 'forgejo-v15-swagger.json')
);

const manifest = [];
const corpus = [];
for (const source of corpusSources) {
  const files = collectFiles(source);
  let sourceBytes = 0;
  for (const file of files) {
    const content = readFileSync(file, 'utf8');
    const bytes = Buffer.byteLength(content);
    sourceBytes += bytes;
    corpus.push({
      source: source.name,
      path: relative(source.root, file),
      bytes,
      sha256: sha256(content),
      content
    });
  }

  manifest.push({
    source: source.name,
    root: source.local ? '.' : relative(workspaceRoot, source.root),
    commit: source.local ? currentCommit(workspaceRoot) : currentCommit(source.root),
    dirty: source.local ? isDirty(workspaceRoot) : isDirty(source.root),
    files: files.length,
    bytes: sourceBytes
  });
}

const routeIndex = buildRouteIndex(join(sourceMetadataRoot, 'forgejo-v15-swagger.json'));
writeFileSync(join(outputRoot, 'corpus.ndjson'), corpus.map((entry) => JSON.stringify(entry)).join('\n') + '\n');
writeFileSync(join(outputRoot, 'manifest.json'), JSON.stringify(manifest, null, 2) + '\n');
writeFileSync(join(outputRoot, 'route-index.json'), JSON.stringify(routeIndex, null, 2) + '\n');
writeFileSync(join(outputRoot, 'summary-api-notes.md'), renderNotes(manifest, routeIndex, corpus));

console.log(`Forgejo research corpus written to ${relative(workspaceRoot, outputRoot)}`);
console.log(`Sources: ${manifest.length}, files: ${corpus.length}, bytes: ${corpus.reduce((sum, entry) => sum + entry.bytes, 0)}`);
console.log(`Route index entries: ${routeIndex.length}`);

function refreshRepo(repo) {
  const checkoutPath = join(repoRoot, repo.name);
  const gitPath = join(checkoutPath, '.git');

  if (!exists(gitPath)) {
    run('git', ['clone', '--depth', '1', repo.cloneUrl, checkoutPath]);
    return;
  }

  if (!noRefresh) {
    run('git', ['-C', checkoutPath, 'pull', '--ff-only']);
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
  const result = run('git', ['-C', root, 'ls-files', '-z']);
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

function renderNotes(manifest, routeIndex, corpus) {
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

  for (const hit of searchCorpus(corpus)) {
    lines.push(`| ${escapeCell(hit.term)} | ${escapeCell(hit.source)} | ${escapeCell(hit.path)} | ${escapeCell(hit.excerpt)} |`);
  }

  lines.push(
    '',
    '## qyl Summary API Surfaces',
    '',
    '- `services/qyl.mcp/Tools/SummaryTools.cs` exposes the MCP tool boundary.',
    '- `services/qyl.mcp/Tools/SummaryFacade.cs` composes collector paths into raw context and optionally delegates to the configured LLM agent.',
    '- Current two-path summaries are error issue + recent events and session + session spans. Trace summary is currently a single collector path over all spans for one trace.',
    ''
  );

  return lines.join('\n');
}

function searchCorpus(corpus) {
  const hits = [];
  for (const term of searchTerms) {
    const lowerTerm = term.toLowerCase();
    for (const entry of corpus) {
      const lowerContent = entry.content.toLowerCase();
      const index = lowerContent.indexOf(lowerTerm);
      if (index < 0) {
        continue;
      }

      hits.push({
        term,
        source: entry.source,
        path: entry.path,
        excerpt: excerpt(entry.content, index, term)
      });

      if (hits.filter((hit) => hit.term === term).length >= 6) {
        break;
      }
    }
  }

  return hits.slice(0, 80);
}

function excerpt(content, index, term) {
  const redacted = redactCredentialText(content);
  const redactedIndex = redacted.toLowerCase().indexOf(term.toLowerCase(), Math.max(0, index - 1_000));
  const effectiveIndex = redactedIndex >= 0 ? redactedIndex : index;
  const start = Math.max(0, effectiveIndex - 90);
  const end = Math.min(redacted.length, effectiveIndex + 180);
  return redacted
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
  return run('git', ['-C', repoPath, 'rev-parse', '--short', 'HEAD']).stdout.trim();
}

function isDirty(repoPath) {
  return run('git', ['-C', repoPath, 'status', '--porcelain']).stdout.trim().length > 0;
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

function run(command, args) {
  const result = spawnSync(command, args, {
    cwd: workspaceRoot,
    encoding: 'utf8',
    stdio: ['ignore', 'pipe', 'pipe']
  });

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
