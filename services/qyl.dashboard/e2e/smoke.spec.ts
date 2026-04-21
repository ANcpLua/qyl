import {expect, test} from '@playwright/test';

test.describe('qyl dashboard smoke tests', () => {
    test('health endpoint returns healthy', async ({request}) => {
        const response = await request.get('/health/ui');
        expect(response.ok()).toBeTruthy();
        const body = await response.json();
        expect(body.status).toBe('healthy');
        expect(body.components).toBeDefined();
    });

    test('landing page loads with sidebar and header', async ({page}) => {
        await page.goto('/');
        // Sidebar navigation should be visible
        await expect(page.getByRole('navigation')).toBeVisible();
        // Page should have a title/header area
        await expect(page.locator('header')).toBeVisible();
    });

    test('sidebar navigation works', async ({page}) => {
        await page.goto('/');

        // Navigate to Traces
        await page.getByRole('link', {name: /traces/i}).click();
        await expect(page).toHaveURL(/\/traces/);

        // Navigate to Logs
        await page.getByRole('link', {name: /logs/i}).click();
        await expect(page).toHaveURL(/\/logs/);

        // Navigate to GenAI
        await page.getByRole('link', {name: /genai/i}).click();
        await expect(page).toHaveURL(/\/genai/);

        // Navigate to Agents
        await page.getByRole('link', {name: /agents/i}).click();
        await expect(page).toHaveURL(/\/agents/);
    });

    test('time range selector is interactive', async ({page}) => {
        await page.goto('/');
        // Time range buttons should be present
        const timeRangeButton = page.getByRole('button', {name: /15m|1h|5m/});
        await expect(timeRangeButton.first()).toBeVisible();
    });

    test('theme toggle switches appearance', async ({page}) => {
        await page.goto('/');
        const html = page.locator('html');

        // Find and click theme toggle
        const themeToggle = page.getByRole('button', {name: /theme|dark|light/i});
        if (await themeToggle.isVisible()) {
            const classBefore = await html.getAttribute('class');
            await themeToggle.click();
            const classAfter = await html.getAttribute('class');
            expect(classBefore).not.toBe(classAfter);
        }
    });

    test('settings page loads', async ({page}) => {
        await page.goto('/settings');
        await expect(page).toHaveURL(/\/settings/);
    });

    test('search input is accessible', async ({page}) => {
        await page.goto('/');
        const searchInput = page.locator('[data-search-input]');
        if (await searchInput.isVisible()) {
            await searchInput.fill('test query');
            await expect(searchInput).toHaveValue('test query');
        }
    });

    test('compatibility endpoints are reachable', async ({request}) => {
        const traces = await request.get('/api/v1/traces?limit=1');
        expect(traces.ok()).toBeTruthy();

        const stats = await request.get('/api/v1/genai/stats');
        expect(stats.ok()).toBeTruthy();

        const spans = await request.get('/api/v1/genai/spans?limit=1');
        expect(spans.ok()).toBeTruthy();

        const search = await request.post('/api/v1/search/query', {
            data: {
                query: 'error',
                limit: 10,
            },
        });
        expect(search.ok()).toBeTruthy();
    });

    test('onboarding verify shows OTLP endpoint and can check telemetry', async ({page}) => {
        await page.goto('/onboarding');

        for (let i = 0; i < 4; i++) {
            await page.getByRole('button', {name: 'NEXT'}).click();
        }

        await expect(page.getByText('EXPECTED ENDPOINT')).toBeVisible();
        await expect(page.getByText('http://localhost:4318')).toBeVisible();

        const verify = page.getByRole('button', {name: /CHECK FOR DATA/i});
        await verify.click();

        await expect(
            page.getByRole('heading', {name: /DATA RECEIVED|NO DATA YET|LISTENING FOR TELEMETRY/i})
        ).toBeVisible();
    });

    test('search query interaction returns 200', async ({page}) => {
        await page.goto('/search');

        const responsePromise = page.waitForResponse(
            (res) => res.url().includes('/api/v1/search/query') && res.request().method() === 'POST'
        );

        const input = page.getByRole('textbox', {name: /Search telemetry data/i});
        await input.fill('error');
        await input.press('Enter');

        const response = await responsePromise;
        expect(response.status()).toBe(200);
    });

    test('logs page opens live stream endpoint', async ({page}) => {
        await page.goto('/logs');

        const response = await page.waitForResponse(
            (res) => res.url().includes('/api/v1/logs/live') && res.request().method() === 'GET',
            {timeout: 10_000}
        );

        expect(response.status()).toBe(200);
    });

    test('keyboard shortcuts navigate routes and open shortcuts modal', async ({page}) => {
        await page.goto('/dashboards/external-apis');

        await page.keyboard.press('a');
        await expect(page).toHaveURL(/\/agents/);

        await page.keyboard.press('b');
        await expect(page).toHaveURL(/\/bot/);

        await page.keyboard.press('/');
        await expect(page).toHaveURL(/\/search/);

        // Blur inputs so global shortcuts are active.
        await page.locator('main').click();
        await page.keyboard.press('Shift+/');

        await expect(page.getByRole('dialog')).toBeVisible();
        await expect(page.getByText('Keyboard Shortcuts')).toBeVisible();
    });

    test('external apis title and sidebar collapse semantics are correct', async ({page}) => {
        await page.goto('/dashboards/external-apis');

        await expect(page.getByRole('heading', {name: 'EXTERNAL APIS'})).toBeVisible();

        const collapse = page.locator('aside button[aria-pressed]').first();
        await expect(collapse).toHaveAttribute('aria-pressed', 'false');
        await collapse.click();
        await expect(collapse).toHaveAttribute('aria-pressed', 'true');
    });
});
