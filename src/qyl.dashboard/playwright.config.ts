import {defineConfig, devices} from '@playwright/test';

export default defineConfig({
    testDir: './e2e',
    fullyParallel: true,
    forbidOnly: !!process.env.CI,
    retries: process.env.CI ? 2 : 0,
    workers: process.env.CI ? 1 : undefined,
    reporter: process.env.CI ? 'github' : 'html',
    use: {
        baseURL: process.env.QYL_BASE_URL || 'http://localhost:5100',
        trace: 'on-first-retry',
        screenshot: 'only-on-failure',
    },
    projects: [
        {
            name: 'chromium',
            use: {...devices['Desktop Chrome']},
        },
    ],
    webServer: process.env.CI
        ? undefined
        : {
            command: 'npm run preview',
            port: 4173,
            reuseExistingServer: true,
        },
});
