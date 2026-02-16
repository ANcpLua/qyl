/**
 * Download utilities for exporting telemetry data as JSON or CSV.
 * Uses browser download API - no backend changes required.
 */

export type ExportFormat = 'json' | 'csv';

/**
 * Generate a timestamped filename for downloads.
 * @param prefix - File name prefix (e.g., 'logs', 'traces', 'genai')
 * @param format - Export format ('json' or 'csv')
 * @returns Filename with timestamp (e.g., 'logs-2024-01-15T10-30-00.json')
 */
export function generateFilename(prefix: string, format: ExportFormat): string {
    const timestamp = new Date().toISOString().replace(/[:.]/g, '-').slice(0, 19);
    return `${prefix}-${timestamp}.${format}`;
}

/**
 * Convert array of objects to CSV string.
 * Handles nested objects by JSON-stringifying them.
 * @param data - Array of objects to convert
 * @param columns - Optional array of column keys to include (defaults to all keys from first item)
 * @returns CSV string with header row
 */
export function convertToCSV<T extends Record<string, unknown>>(
    data: T[],
    columns?: string[]
): string {
    if (data.length === 0) {
        return '';
    }

    // Determine columns from first item if not specified
    const cols = columns ?? Object.keys(data[0]);

    // Build header row
    const header = cols.map((col) => escapeCSVValue(col)).join(',');

    // Build data rows
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

/**
 * Format a value for CSV output.
 * Converts objects/arrays to JSON, handles null/undefined.
 */
function formatValue(value: unknown): string {
    if (value === null || value === undefined) {
        return '';
    }
    if (typeof value === 'object') {
        return JSON.stringify(value);
    }
    return String(value);
}

/**
 * Escape a value for CSV (handles quotes and commas).
 */
function escapeCSVValue(value: string): string {
    // If value contains comma, newline, or quote, wrap in quotes and escape internal quotes
    if (value.includes(',') || value.includes('\n') || value.includes('"')) {
        return `"${value.replace(/"/g, '""')}"`;
    }
    return value;
}

/**
 * Trigger browser download of content as a file.
 * @param content - String content to download
 * @param filename - Name for the downloaded file
 * @param mimeType - MIME type for the file
 */
export function downloadFile(content: string, filename: string, mimeType: string): void {
    const blob = new Blob([content], {type: mimeType});
    const url = URL.createObjectURL(blob);

    const link = document.createElement('a');
    link.href = url;
    link.download = filename;

    // Append to body, click, and remove
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);

    // Clean up object URL
    URL.revokeObjectURL(url);
}

/**
 * Export data as JSON file.
 * @param data - Data to export (will be JSON.stringify'd with indentation)
 * @param filenamePrefix - Prefix for the filename
 */
export function downloadAsJSON<T>(data: T, filenamePrefix: string): void {
    const content = JSON.stringify(data, null, 2);
    const filename = generateFilename(filenamePrefix, 'json');
    downloadFile(content, filename, 'application/json');
}

/**
 * Export data as CSV file.
 * @param data - Array of objects to export
 * @param filenamePrefix - Prefix for the filename
 * @param columns - Optional array of column keys to include
 */
export function downloadAsCSV<T extends Record<string, unknown>>(
    data: T[],
    filenamePrefix: string,
    columns?: string[]
): void {
    const content = convertToCSV(data, columns);
    const filename = generateFilename(filenamePrefix, 'csv');
    downloadFile(content, filename, 'text/csv');
}

/**
 * Export data in the specified format.
 * @param data - Data to export
 * @param format - Export format ('json' or 'csv')
 * @param filenamePrefix - Prefix for the filename
 * @param columns - Optional array of column keys for CSV export
 */
export function downloadData<T extends Record<string, unknown>>(
    data: T[],
    format: ExportFormat,
    filenamePrefix: string,
    columns?: string[]
): void {
    if (format === 'json') {
        downloadAsJSON(data, filenamePrefix);
    } else {
        downloadAsCSV(data, filenamePrefix, columns);
    }
}
