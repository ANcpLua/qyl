import crypto from 'node:crypto';
import fs from 'node:fs';
import path from 'node:path';
import process from 'node:process';
import {execFileSync} from 'node:child_process';
import {createRequire} from 'node:module';
import {fileURLToPath} from 'node:url';

const scriptPath = fileURLToPath(import.meta.url);
const repositoryRoot = path.resolve(path.dirname(scriptPath), '../..');
const require = createRequire(path.join(repositoryRoot, 'services/qyl.dashboard/package.json'));
const ts = require('typescript');
const sourceRoots = ['eng', 'packages', 'services', 'tests'];
const sourceExtensions = new Set(['.cts', '.mts', '.ts', '.tsx']);
const excludedDirectories = new Set([
    '.vite',
    'artifacts',
    'bin',
    'build',
    'coverage',
    'dist',
    'generated',
    'node_modules',
    'obj',
    'playwright-report',
    'test-results',
]);
const frontendPackages = [
    'packages/Qyl.Host.Console',
    'services/qyl.dashboard',
];
const relevantCompilerOptions = [
    'strict',
    'noCheck',
    'noImplicitAny',
    'noImplicitThis',
    'strictNullChecks',
    'strictFunctionTypes',
    'strictBindCallApply',
    'strictPropertyInitialization',
    'alwaysStrict',
    'useUnknownInCatchVariables',
    'strictBuiltinIteratorReturn',
    'allowJs',
    'checkJs',
    'skipLibCheck',
    'skipDefaultLibCheck',
    'exactOptionalPropertyTypes',
    'noUncheckedIndexedAccess',
    'suppressImplicitAnyIndexErrors',
];

function git(args, encoding = 'utf8') {
    return execFileSync('git', ['-C', repositoryRoot, ...args], {
        encoding,
        maxBuffer: 64 * 1024 * 1024,
        stdio: ['ignore', 'pipe', 'pipe'],
    });
}

function parseArguments(args) {
    let ref;
    let format = 'json';
    for (let index = 0; index < args.length; index++) {
        const argument = args[index];
        if (argument === '--ref') {
            ref = requireValue(args, ++index, argument);
        } else if (argument === '--format') {
            format = requireValue(args, ++index, argument);
        } else if (argument === '--help') {
            printHelp();
            process.exit(0);
        } else {
            throw new Error(`Unknown argument: ${argument}`);
        }
    }
    if (!ref) throw new Error('--ref is required');
    if (!['json', 'summary', 'tsv'].includes(format)) {
        throw new Error(`Unsupported format: ${format}`);
    }
    return {ref, format};
}

function requireValue(args, index, argument) {
    const value = args[index];
    if (!value) throw new Error(`Missing value for ${argument}`);
    return value;
}

function printHelp() {
    process.stdout.write(
        'Usage: node eng/scripts/typescript-type-escape-audit.mjs ' +
        '--ref <git-commit> [--format json|summary|tsv]\n',
    );
}

function resolveCommit(ref) {
    return git(['rev-parse', '--verify', `${ref}^{commit}`]).trim();
}

function readBlob(commit, file) {
    return git(['show', `${commit}:${file}`]);
}

function listTrackedFiles(commit) {
    const output = git(['ls-tree', '-r', '--name-only', '-z', commit, '--', ...sourceRoots], 'buffer');
    return output.toString('utf8').split('\0').filter(Boolean).sort();
}

function isExcluded(file) {
    return file.split('/').some(part => excludedDirectories.has(part.toLowerCase()));
}

function isSourceFile(file) {
    return sourceRoots.some(root => file === root || file.startsWith(`${root}/`)) &&
        sourceExtensions.has(path.extname(file)) &&
        !isExcluded(file);
}

function isTestFile(file) {
    return /(?:\.test\.|\.spec\.|\/e2e\/)/.test(file);
}

function scriptKind(file) {
    return file.endsWith('.tsx') ? ts.ScriptKind.TSX : ts.ScriptKind.TS;
}

function position(sourceFile, offset) {
    const value = sourceFile.getLineAndCharacterOfPosition(offset);
    return {line: value.line + 1, column: value.character + 1};
}

function preview(text) {
    const normalized = text.replace(/\s+/g, ' ').trim();
    return normalized.length <= 240 ? normalized : `${normalized.slice(0, 237)}...`;
}

function site(sourceFile, file, node, extra = {}) {
    const start = node.getStart(sourceFile);
    return {
        path: file,
        ...position(sourceFile, start),
        scope: isTestFile(file) ? 'test' : 'product',
        syntaxKind: ts.SyntaxKind[node.kind],
        preview: preview(node.getText(sourceFile)),
        ...extra,
    };
}

function scanSource(file, source) {
    const sourceFile = ts.createSourceFile(
        file,
        source,
        ts.ScriptTarget.Latest,
        true,
        scriptKind(file),
    );
    const typeAssertions = [];
    const doubleAssertions = [];
    const nonNullAssertions = [];
    const explicitAny = [];

    function visit(node) {
        if (ts.isAsExpression(node) || ts.isTypeAssertionExpression(node)) {
            typeAssertions.push(site(sourceFile, file, node, {
                assertedType: node.type.getText(sourceFile),
            }));
            if (ts.isAsExpression(node) &&
                node.type.kind === ts.SyntaxKind.UnknownKeyword &&
                ts.isAsExpression(node.parent)) {
                doubleAssertions.push(site(sourceFile, file, node.parent, {
                    innerAssertedType: node.type.getText(sourceFile),
                    outerAssertedType: node.parent.type.getText(sourceFile),
                }));
            }
        } else if (ts.isNonNullExpression(node)) {
            nonNullAssertions.push(site(sourceFile, file, node));
        } else if (node.kind === ts.SyntaxKind.AnyKeyword) {
            explicitAny.push(site(sourceFile, file, node));
        }
        ts.forEachChild(node, visit);
    }

    visit(sourceFile);
    return {
        typeAssertions,
        doubleAssertions,
        nonNullAssertions,
        explicitAny,
        suppressions: scanSuppressions(sourceFile, file, source),
    };
}

function scanSuppressions(sourceFile, file, source) {
    const suppressions = [];
    const scanner = ts.createScanner(ts.ScriptTarget.Latest, false, scriptKind(file), source);
    for (let token = scanner.scan(); token !== ts.SyntaxKind.EndOfFileToken; token = scanner.scan()) {
        if (token !== ts.SyntaxKind.SingleLineCommentTrivia &&
            token !== ts.SyntaxKind.MultiLineCommentTrivia) continue;
        const comment = scanner.getTokenText();
        for (const match of comment.matchAll(/@ts-(ignore|expect-error|nocheck)\b/g)) {
            const directive = match[1];
            suppressions.push({
                path: file,
                ...position(sourceFile, scanner.getTokenPos() + match.index),
                scope: isTestFile(file) ? 'test' : 'product',
                directive,
                preview: match[0],
            });
        }
    }
    return suppressions;
}

function flatten(sites, key) {
    return sites.flatMap(value => value[key]);
}

function readJson(commit, file) {
    return JSON.parse(readBlob(commit, file));
}

function verifyTypeScriptVersions(commit) {
    const versions = frontendPackages.map(packageDirectory => {
        const manifest = readJson(commit, `${packageDirectory}/package.json`);
        const lock = readJson(commit, `${packageDirectory}/package-lock.json`);
        const declared = manifest.devDependencies?.typescript;
        const locked = lock.packages?.['node_modules/typescript']?.version;
        if (typeof declared !== 'string' || typeof locked !== 'string') {
            throw new Error(`TypeScript is not pinned in ${packageDirectory}`);
        }
        if (declared !== ts.version || locked !== ts.version) {
            throw new Error(
                `TypeScript version mismatch in ${packageDirectory}: ` +
                `loaded=${ts.version}, declared=${declared}, locked=${locked}`,
            );
        }
        return {package: packageDirectory, declared, locked};
    });
    return versions;
}

function compilerConfigs(commit, trackedFiles) {
    return trackedFiles
        .filter(file => /(?:^|\/)tsconfig[^/]*\.json$/.test(file) && !isExcluded(file))
        .map(file => {
            const source = readBlob(commit, file);
            const parsed = ts.parseConfigFileTextToJson(file, source);
            if (parsed.error) {
                throw new Error(ts.flattenDiagnosticMessageText(parsed.error.messageText, '\n'));
            }
            if (parsed.config.extends !== undefined) {
                throw new Error(`Unsupported tsconfig extends in ${file}`);
            }
            const declared = parsed.config.compilerOptions ?? {};
            return {
                path: file,
                options: Object.fromEntries(
                    relevantCompilerOptions
                        .filter(option => Object.hasOwn(declared, option))
                        .map(option => [option, declared[option]]),
                ),
            };
        });
}

function scriptHash() {
    return crypto.createHash('sha256').update(fs.readFileSync(scriptPath)).digest('hex');
}

function buildResult(ref) {
    const commit = resolveCommit(ref);
    const tree = git(['rev-parse', `${commit}^{tree}`]).trim();
    const trackedFiles = listTrackedFiles(commit);
    const scannedFiles = trackedFiles.filter(isSourceFile);
    const scanned = scannedFiles.map(file => scanSource(file, readBlob(commit, file)));
    const typeAssertions = flatten(scanned, 'typeAssertions');
    const doubleAssertions = flatten(scanned, 'doubleAssertions');
    const nonNullAssertions = flatten(scanned, 'nonNullAssertions');
    const explicitAny = flatten(scanned, 'explicitAny');
    const suppressions = flatten(scanned, 'suppressions');
    return {
        schemaVersion: 1,
        commit,
        tree,
        auditor: {
            path: path.relative(repositoryRoot, scriptPath).split(path.sep).join('/'),
            sha256: scriptHash(),
        },
        typescriptVersion: ts.version,
        typeScriptPackages: verifyTypeScriptVersions(commit),
        scope: {
            roots: sourceRoots,
            extensions: [...sourceExtensions],
            excludedDirectories: [...excludedDirectories].sort(),
        },
        filesScanned: scannedFiles.length,
        scannedFiles,
        compilerConfigs: compilerConfigs(commit, trackedFiles),
        summary: {
            typeAssertionNodes: typeAssertions.length,
            productTypeAssertionNodes: typeAssertions.filter(value => value.scope === 'product').length,
            testTypeAssertionNodes: typeAssertions.filter(value => value.scope === 'test').length,
            doubleAssertionChains: doubleAssertions.length,
            nonNullAssertionNodes: nonNullAssertions.length,
            explicitAnyKeywords: explicitAny.length,
            tsIgnoreDirectives: suppressions.filter(value => value.directive === 'ignore').length,
            tsExpectErrorDirectives: suppressions.filter(value => value.directive === 'expect-error').length,
            tsNoCheckDirectives: suppressions.filter(value => value.directive === 'nocheck').length,
        },
        sites: {
            typeAssertions,
            doubleAssertions,
            nonNullAssertions,
            explicitAny,
            suppressions,
        },
    };
}

function printSummary(result) {
    const summary = result.summary;
    process.stdout.write([
        `commit=${result.commit}`,
        `tree=${result.tree}`,
        `auditor_sha256=${result.auditor.sha256}`,
        `typescript=${result.typescriptVersion}`,
        `files=${result.filesScanned}`,
        `assertion_nodes=${summary.typeAssertionNodes}`,
        `production_assertion_nodes=${summary.productTypeAssertionNodes}`,
        `test_assertion_nodes=${summary.testTypeAssertionNodes}`,
        `double_assertion_chains=${summary.doubleAssertionChains}`,
        `non_null_assertions=${summary.nonNullAssertionNodes}`,
        `explicit_any_nodes=${summary.explicitAnyKeywords}`,
        `ts_ignore_directives=${summary.tsIgnoreDirectives}`,
        `ts_expect_error_directives=${summary.tsExpectErrorDirectives}`,
        `ts_nocheck_directives=${summary.tsNoCheckDirectives}`,
        '',
    ].join('\n'));
}

function printTsv(result) {
    process.stdout.write('kind\tscope\tpath\tline\tcolumn\ttarget\tpreview\n');
    const groups = [
        ['assertion', result.sites.typeAssertions],
        ['non_null', result.sites.nonNullAssertions],
        ['explicit_any', result.sites.explicitAny],
        ['ts_directive', result.sites.suppressions],
    ];
    for (const [kind, sites] of groups) {
        for (const value of sites) {
            process.stdout.write([
                kind,
                value.scope,
                value.path,
                value.line,
                value.column,
                value.assertedType ?? value.directive ?? '',
                value.preview.replace(/\t/g, ' '),
            ].join('\t') + '\n');
        }
    }
}

const options = parseArguments(process.argv.slice(2));
const result = buildResult(options.ref);
if (options.format === 'summary') {
    printSummary(result);
} else if (options.format === 'tsv') {
    printTsv(result);
} else {
    process.stdout.write(`${JSON.stringify(result, null, 2)}\n`);
}
