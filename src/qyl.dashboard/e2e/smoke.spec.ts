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
});
