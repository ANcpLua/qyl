import {randomBytes} from 'node:crypto';
import {expect, test, type APIRequestContext} from '@playwright/test';

async function seedHistory(request: APIRequestContext, marker: string) {
    const start = BigInt(Date.now() - 10_000) * 1_000_000n;
    const record = (index: number, body: string, severityNumber = 9) => ({
        timeUnixNano: String(start + BigInt(index) * 1_000_000n),
        observedTimeUnixNano: String(start + BigInt(index) * 1_000_000n),
        severityNumber,
        severityText: severityNumber === 17 ? 'ERROR' : 'INFO',
        body: {stringValue: body},
    });
    const recent = Array.from({length: 501}, (_, index) => record(index + 2, `${marker} recent ${index}`));
    // Equal timestamps must not collapse distinct events during pagination.
    recent[101] = {...recent[100], body: {stringValue: `${marker} recent 101`}};
    const response = await request.post('/v1/logs', {
        data: {
            resourceLogs: [
                {
                    resource: {attributes: [{key: 'service.name', value: {stringValue: `${marker}-recent`}}]},
                    scopeLogs: [{logRecords: recent}],
                },
                {
                    resource: {attributes: [{key: 'service.name', value: {stringValue: `${marker}-older`}}]},
                    scopeLogs: [{logRecords: [
                        record(0, `${marker} older error one`, 17),
                        record(1, `${marker} older error two`, 17),
                    ]}],
                },
            ],
        },
    });
    expect(response.status()).toBe(200);
    await expect.poll(async () => {
        const logs = await request.get(`/api/v1/logs?query=${marker}&limit=1000`);
        return (await logs.json() as {items: unknown[]}).items.length;
    }).toBe(503);
}

test.describe('historical logs', () => {
    test.beforeEach(() => {
        test.skip(Boolean(process.env.QYL_BASE_URL), 'Log fixtures belong to the local ephemeral collector.');
    });

    test('uses observation time when an event timestamp is absent and keeps the raw timestamp', async ({page, request}) => {
        const marker = `observation-${randomBytes(6).toString('hex')}`;
        const observed = Date.now();
        const response = await request.post('/v1/logs', {data: {resourceLogs: [{
            resource: {attributes: [{key: 'service.name', value: {stringValue: marker}}]},
            scopeLogs: [{logRecords: [{timeUnixNano: '0', observedTimeUnixNano: String(BigInt(observed) * 1_000_000n),
                severityNumber: 13, severityText: 'NOTICE/remote', body: {stringValue: marker}}]}],
        }]}});
        expect(response.status()).toBe(200);
        await page.goto('/logs');
        await page.getByRole('textbox', {name: 'Search logs', exact: true}).fill(marker);
        const row = page.getByRole('button', {name: new RegExp(marker)});
        await expect(row).toBeVisible();
        await expect(row.getByTitle('Observed time (event time not supplied)')).toHaveText(
            new Date(observed).toLocaleTimeString('en-US', {hour12: false, hour: '2-digit', minute: '2-digit', second: '2-digit', fractionalSecondDigits: 3}));
        await expect(row.getByTitle('NOTICE/remote')).toBeVisible();
        const stored = await request.get(`/api/v1/logs?query=${marker}`);
        expect((await stored.json()).items[0].time_unix_nano).toBe('0');
    });

    test('error filtering queries stored records before any older pages are loaded', async ({page, request}) => {
        const marker = `history-${randomBytes(6).toString('hex')}`;
        await seedHistory(request, marker);
        await page.goto('/logs');
        await page.getByRole('textbox', {name: 'Search logs', exact: true}).fill(marker);
        await page.getByRole('combobox').first().click();
        await page.getByRole('option', {name: 'ERROR', exact: true}).click();
        await expect(page.getByRole('button', {name: new RegExp(`${marker} older error one`)})).toBeVisible();
        await expect(page.getByRole('button', {name: new RegExp(`${marker} older error two`)})).toBeVisible();
        await expect(page.getByRole('status', {name: 'Log results'})).toContainText('2 logs loaded');
    });

    test('pages through all records and finds errors and services outside the recent tail', async ({page, request}) => {
        const marker = `history-${randomBytes(6).toString('hex')}`;
        await seedHistory(request, marker);
        await page.goto('/logs');
        await page.getByRole('textbox', {name: 'Search logs', exact: true}).fill(marker);
        await expect(page.getByRole('status', {name: 'Log results'})).toContainText('250 logs loaded');
        await page.getByRole('button', {name: 'Load older', exact: true}).click();
        await expect(page.getByRole('status', {name: 'Log results'})).toContainText('500 logs loaded');
        await page.getByRole('button', {name: 'Load older', exact: true}).click();
        await expect(page.getByRole('status', {name: 'Log results'})).toContainText('503 logs loaded');
        await expect(page.getByText('All matching logs loaded', {exact: true})).toBeVisible();
        await page.getByRole('combobox', {name: 'Minimum log level'}).click();
        await page.getByRole('option', {name: 'ERROR', exact: true}).click();
        await expect(page.getByRole('status', {name: 'Log results'})).toContainText('2 logs loaded');
        await expect(page.getByRole('button', {name: new RegExp(`${marker} older error one`)})).toBeVisible();
        await expect(page.getByRole('button', {name: new RegExp(`${marker} older error two`)})).toBeVisible();
        await page.getByRole('textbox', {name: 'Filter service', exact: true}).fill(`${marker}-older`);
        await expect(page.getByRole('status', {name: 'Log results'})).toContainText('2 logs loaded');
        await page.getByRole('button', {name: 'Clear minimum level filter', exact: true}).click();
        await expect(page.getByRole('status', {name: 'Log results'})).toContainText('2 logs loaded');
        await page.getByRole('button', {name: 'Clear service filter', exact: true}).click();
        await expect(page.getByRole('status', {name: 'Log results'})).toContainText('503 logs loaded');
        await expect(page.getByText('All matching logs loaded', {exact: true})).toBeVisible();
    });

    test('shows page failures without dropping loaded records and retries the same continuation', async ({page, request}) => {
        const marker = `history-${randomBytes(6).toString('hex')}`;
        await seedHistory(request, marker);
        let failNextPage = true;
        await page.route('**/api/v1/logs?**', async route => {
            if (new URL(route.request().url()).searchParams.has('cursor') && failNextPage) {
                failNextPage = false;
                await route.fulfill({
                    status: 503,
                    contentType: 'application/problem+json',
                    body: JSON.stringify({
                        type: 'https://qyl.at/problems/service-unavailable',
                        code: 'service_unavailable',
                        title: 'Page unavailable',
                        status: 503,
                        detail: 'Injected page failure',
                    }),
                });
            } else {
                await route.continue();
            }
        });
        await page.goto('/logs');
        await page.getByRole('textbox', {name: 'Search logs', exact: true}).fill(marker);
        await expect(page.getByRole('status', {name: 'Log results'})).toContainText('250 logs loaded');
        await page.getByRole('button', {name: 'Load older', exact: true}).click();
        await expect(page.getByRole('alert')).toContainText('Injected page failure');
        await expect(page.getByRole('status', {name: 'Log results'})).toContainText('250 logs loaded');
        await page.getByRole('button', {name: 'Load older', exact: true}).click();
        await expect(page.getByRole('status', {name: 'Log results'})).toContainText('500 logs loaded');
        await expect(page.getByRole('alert')).toHaveCount(0);
    });
});
