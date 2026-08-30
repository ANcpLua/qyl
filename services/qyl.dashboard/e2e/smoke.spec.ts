import {execFile} from 'node:child_process';
import {randomBytes} from 'node:crypto';
import {promisify} from 'node:util';
import {expect, test} from '@playwright/test';
import {publishedContractSchema} from '@ancplua/qyl-api-schema/zod';
import {
    ProblemDetailsMediaType,
    type HealthReport,
    type LogRecord,
    type NotFoundError,
    type Trace,
    type ValidationError,
} from '@ancplua/qyl-api-schema/types';
import {parseContract} from '../src/lib/contract-validation';

const execFileAsync = promisify(execFile);

function contractParser<T>(definitionName: string): (value: unknown) => T {
    const schema = publishedContractSchema<T>(definitionName);
    return value => parseContract(schema, value, definitionName);
}

const parseTraceContract = contractParser<Trace>('OTel.Traces.Trace');
const parseLogContract = contractParser<LogRecord>('OTel.Logs.LogRecord');
const parseHealthContract = contractParser<HealthReport>('Health.HealthReport');
const parseNotFoundContract = contractParser<NotFoundError>('Common.Errors.NotFoundError');
const parseValidationErrorContract = contractParser<ValidationError>('Common.Errors.ValidationError');

function pageItems(value: unknown): unknown[] {
    expect(value).toEqual(expect.objectContaining({items: expect.any(Array), has_more: expect.any(Boolean)}));
    return (value as {items: unknown[]}).items;
}

function generatedPageItems<T>(parseItem: (value: unknown) => T, value: unknown): T[] {
    return pageItems(value).map(item => parseItem(item));
}

test.describe('qyl executable product surface', () => {
    test('readiness is a real collector health report', async ({request}) => {
        const response = await request.get('/health');
        expect(response.status()).toBe(200);
        expect(response.headers()['content-type']).toContain('application/json');

        const body = parseHealthContract(await response.json());
        expect(body.status).toBe('healthy');
        expect(body.entries.duckdb.status).toBe('healthy');
    });

    test('root redirects to the traces product surface', async ({page}) => {
        await page.goto('/');
        await expect(page).toHaveURL(/\/traces$/);
        await expect(page.getByRole('navigation', {name: 'Main navigation'})).toBeVisible();
        await expect(page.getByRole('heading', {name: 'TRACES'})).toBeVisible();
    });

    test('only shipped navigation entries are exposed', async ({page}) => {
        await page.goto('/traces');

        await expect(page.getByRole('link', {name: /traces/i})).toBeVisible();
        await expect(page.getByRole('link', {name: /logs/i})).toBeVisible();
        await expect(page.getByRole('link', {name: /agents/i})).toHaveCount(0);
        await expect(page.getByRole('link', {name: /search/i})).toHaveCount(0);
    });

    test('official SDK OTLP/protobuf is persisted and returned through generated product contracts', async ({request}) => {
        test.setTimeout(90_000);
        test.skip(Boolean(process.env.QYL_BASE_URL), 'Telemetry mutation is limited to the local ephemeral collector.');

        const serviceName = `qyl-e2e-${randomBytes(6).toString('hex')}`;
        const collector = 'http://127.0.0.1:5100';

        await execFileAsync(
            'dotnet',
            [
                'run',
                '--project',
                '../../packages/Qyl.Run.Workload/Qyl.Run.Workload.csproj',
                '--configuration',
                'Release',
                '--no-launch-profile',
            ],
            {
                cwd: process.cwd(),
                timeout: 75_000,
                env: {
                    ...process.env,
                    ASPNETCORE_URLS: 'http://127.0.0.1:0',
                    OTEL_EXPORTER_OTLP_ENDPOINT: collector,
                    OTEL_EXPORTER_OTLP_PROTOCOL: 'http/protobuf',
                    OTEL_SERVICE_NAME: serviceName,
                    QYL_WORKLOAD_ONESHOT: '1',
                },
            },
        );

        await expect.poll(async () => {
            const response = await request.get('/api/v1/traces?limit=100');
            expect(response.status()).toBe(200);
            const traces = generatedPageItems(parseTraceContract, await response.json());
            return traces.find(trace => trace.services.includes(serviceName));
        }).toMatchObject({
            services: [serviceName],
        });

        await expect.poll(async () => {
            const response = await request.get(
                `/api/v1/logs?serviceName=${encodeURIComponent(serviceName)}&limit=100`,
            );
            expect(response.status()).toBe(200);
            const logs = generatedPageItems(parseLogContract, await response.json());
            return logs.find(log => log.resource.service_name === serviceName)?.body.string_value;
        }, {timeout: 20_000}).toMatch(/^(?!sha256:).+\S.*$/);
    });

    test('deleted speculative API is not silently served by the SPA', async ({request}) => {
        const response = await request.post('/api/v1/search/query', {data: {query: 'error'}});
        expect(response.status()).toBe(404);
        expect(response.headers()['content-type'] ?? '').not.toContain('text/html');

        for (const protocolPath of ['/v1', '/v1development/nope']) {
            const protocolResponse = await request.get(protocolPath);
            expect(protocolResponse.status()).toBe(404);
            expect(protocolResponse.headers()['content-type'] ?? '').not.toContain('text/html');
        }
    });

    test('protobuf requests keep protobuf error envelopes', async ({request}) => {
        const response = await request.post('http://127.0.0.1:5100/v1/traces', {
            headers: {
                'content-type': 'application/x-protobuf',
                'content-encoding': 'br',
            },
            data: Buffer.alloc(0),
        });

        expect(response.status()).toBe(415);
        expect(response.headers()['content-type']).toContain('application/x-protobuf');
    });

    test('product errors use the generated Problem Details contract and media type', async ({request}) => {
        const response = await request.get('/api/v1/traces/00000000000000000000000000000000');
        expect(response.status()).toBe(404);
        expect(response.headers()['content-type']).toContain(ProblemDetailsMediaType);
        const problem = parseNotFoundContract(await response.json());
        expect(problem.status).toBe(404);
    });

    test('malformed typed queries use the generated validation contract', async ({request}) => {
        for (const [path, field, code] of [
            ['/api/v1/sessions?isActive=perhaps', 'isActive', 'query.invalid_boolean'],
            ['/api/v1/traces?limit=many', 'limit', 'query.invalid_integer'],
            ['/api/v1/logs?startTime=yesterday', 'startTime', 'query.invalid_date_time'],
        ] as const) {
            const response = await request.get(path);
            expect(response.status()).toBe(400);
            expect(response.headers()['content-type']).toContain(ProblemDetailsMediaType);
            const problem = parseValidationErrorContract(await response.json());
            expect(problem.errors).toEqual([
                expect.objectContaining({field, code}),
            ]);
        }
    });

    test('logs page connects to the real SSE route', async ({page}) => {
        const stream = page.waitForResponse(
            response => response.url().includes('/api/v1/stream/logs')
                && response.request().method() === 'GET',
        );

        await page.goto('/logs');
        expect((await stream).status()).toBe(200);
    });
});
