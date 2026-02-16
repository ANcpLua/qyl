import {useState} from 'react';
import {ChevronDown, Download, FileJson, FileSpreadsheet} from 'lucide-react';
import {Button} from './button';
import {DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger,} from './dropdown-menu';
import {downloadData, type ExportFormat} from '@/lib/download';

export interface DownloadButtonProps<T extends Record<string, unknown>> {
    /** Function that returns the data to export (called when user clicks download) */
    getData: () => T[];
    /** Prefix for the filename (e.g., 'logs', 'traces', 'genai-spans') */
    filenamePrefix: string;
    /** Optional array of column keys to include in CSV export */
    columns?: string[];
    /** Button variant */
    variant?: 'default' | 'outline' | 'secondary' | 'ghost';
    /** Button size */
    size?: 'default' | 'sm' | 'lg' | 'icon';
    /** Whether the button is disabled */
    disabled?: boolean;
    /** Custom class name */
    className?: string;
}

/**
 * Download button with dropdown for format selection (JSON/CSV).
 * Uses browser download API - no backend changes required.
 */
export function DownloadButton<T extends Record<string, unknown>>({
                                                                      getData,
                                                                      filenamePrefix,
                                                                      columns,
                                                                      variant = 'outline',
                                                                      size = 'sm',
                                                                      disabled = false,
                                                                      className,
                                                                  }: DownloadButtonProps<T>) {
    const [isDownloading, setIsDownloading] = useState(false);

    const handleDownload = async (format: ExportFormat) => {
        setIsDownloading(true);
        try {
            const data = getData();
            if (data.length === 0) {
                // Could show a toast here, but for now just skip
                return;
            }
            downloadData(data, format, filenamePrefix, columns);
        } finally {
            setIsDownloading(false);
        }
    };

    return (
        <DropdownMenu>
            <DropdownMenuTrigger asChild>
                <Button
                    variant={variant}
                    size={size}
                    disabled={disabled || isDownloading}
                    className={className}
                >
                    <Download className="w-4 h-4 mr-2"/>
                    Download
                    <ChevronDown className="w-3 h-3 ml-1"/>
                </Button>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end">
                <DropdownMenuItem onClick={() => handleDownload('json')}>
                    <FileJson className="w-4 h-4 mr-2"/>
                    Download as JSON
                </DropdownMenuItem>
                <DropdownMenuItem onClick={() => handleDownload('csv')}>
                    <FileSpreadsheet className="w-4 h-4 mr-2"/>
                    Download as CSV
                </DropdownMenuItem>
            </DropdownMenuContent>
        </DropdownMenu>
    );
}
