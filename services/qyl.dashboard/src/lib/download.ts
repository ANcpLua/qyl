export type ExportFormat = 'json' | 'csv';

function generateFilename(prefix: string, format: ExportFormat): string {
    const timestamp = new Date().toISOString().replace(/[:.]/g, '-').slice(0, 19);
    return `${prefix}-${timestamp}.${format}`;
}

function convertToCSV<T extends Record<string, unknown>>(
    data: readonly T[],
    columns?: string[]
): string {
    if (data.length === 0) {
        return '';
    }

    const cols = columns ?? Object.keys(data[0]);

    const header = cols.map((col) => escapeCSVValue(col)).join(',');

    const rows = data.map((item) => {
        return cols
            .map((col) => {
                const value = item[col];
                return escapeCSVValue(formatValue(value));
            })
            .join(',');
    });

    return [header, ...rows].join('\n');
}

function formatValue(value: unknown): string {
    if (value === null || value === undefined) {
        return '';
    }
    if (typeof value === 'object') {
        return JSON.stringify(value);
    }
    return String(value);
}

function escapeCSVValue(value: string): string {
    if (value.includes(',') || value.includes('\n') || value.includes('"')) {
        return `"${value.replace(/"/g, '""')}"`;
    }
    return value;
}

function downloadFile(content: string, filename: string, mimeType: string): void {
    const blob = new Blob([content], {type: mimeType});
    const url = URL.createObjectURL(blob);

    const link = document.createElement('a');
    link.href = url;
    link.download = filename;

    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);

    URL.revokeObjectURL(url);
}

export function downloadJson<T>(data: readonly T[], filenamePrefix: string): void {
    const content = JSON.stringify(data, null, 2);
    const filename = generateFilename(filenamePrefix, 'json');
    downloadFile(content, filename, 'application/json');
}

export function downloadCsv<T extends Record<string, unknown>>(
    data: readonly T[],
    filenamePrefix: string,
    columns?: string[]
): void {
    const content = convertToCSV(data, columns);
    const filename = generateFilename(filenamePrefix, 'csv');
    downloadFile(content, filename, 'text/csv');
}
