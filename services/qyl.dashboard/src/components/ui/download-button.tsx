import {useState} from 'react';
import {ChevronDown, Download, FileJson, FileSpreadsheet} from 'lucide-react';
import {Button} from './button';
import {DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger,} from './dropdown-menu';
import {downloadData, type ExportFormat} from '@/lib/download';

export interface DownloadButtonProps<T extends Record<string, unknown>> {
    getData: () => T[];
    filenamePrefix: string;
    columns?: string[];
    variant?: 'default' | 'outline' | 'secondary' | 'ghost';
    size?: 'default' | 'sm' | 'lg' | 'icon';
    disabled?: boolean;
    className?: string;
}

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
                return;
            }
            downloadData(data, format, filenamePrefix, columns);
        } finally {
            setIsDownloading(false);
        }
    };

    return (
        <DropdownMenu>
            <DropdownMenuTrigger
                render={<Button
                    variant={variant}
                    size={size}
                    disabled={disabled || isDownloading}
                    className={className}
                />}
            >
                <Download className="w-4 h-4 mr-2"/>
                Download
                <ChevronDown className="w-3 h-3 ml-1"/>
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
