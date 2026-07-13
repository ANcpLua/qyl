import {defineConfig, devices} from '@playwright/test';

export default defineConfig({
    testDir: './e2e',
    fullyParallel: true,
    forbidOnly: !!process.env.CI,
    retries: process.env.CI ? 2 : 0,
    workers: process.env.CI ? 1 : undefined,
    reporter: process.env.CI ? 'github' : 'html',
    use: {
        baseURL: process.env.QYL_BASE_URL || 'http://127.0.0.1:5100',
        trace: 'on-first-retry',
        screenshot: 'only-on-failure',
    },
    projects: [
        {
            name: 'chromium',
            use: {...devices['Desktop Chrome']},
        },
    ],
    webServer: process.env.QYL_BASE_URL
        ? undefined
        : {
            // Build and run the actual single-origin Release product. This deliberately exercises
            // embedded-dashboard middleware before routing; a SPA that shadows OTLP/API paths fails.
            command: 'dotnet run --project ../qyl.collector --configuration Release --no-restore -p:QylEmbedDashboard=true',
            url: 'http://127.0.0.1:5100/health',
            timeout: 120_000,
            reuseExistingServer: false,
            env: {
                ...process.env,
                ASPNETCORE_ENVIRONMENT: 'Development',
                QYL_BIND_ADDRESS: '127.0.0.1',
                QYL_PORT: '5100',
                QYL_OTLP_PORT: '0',
                QYL_GRPC_PORT: '0',
                QYL_DATA_PATH: ':memory:',
                QYL_OTLP_AUTH_MODE: 'Unsecured',
            },
        },
});
