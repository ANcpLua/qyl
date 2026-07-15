import {useState} from 'react';
import {ChevronDown, Download, FileJson, FileSpreadsheet} from 'lucide-react';
import {Button} from './button';
import {DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger,} from './dropdown-menu';
import {downloadCsv, downloadJson, type ExportFormat} from '@/lib/download';

export interface DownloadButtonProps<TJson, TCsv extends Record<string, unknown>> {
    getJsonData: () => readonly TJson[];
    getCsvData: () => readonly TCsv[];
    filenamePrefix: string;
    columns?: string[];
    variant?: 'default' | 'outline' | 'secondary' | 'ghost';
    size?: 'default' | 'sm' | 'lg' | 'icon';
    disabled?: boolean;
    className?: string;
}

export function DownloadButton<TJson, TCsv extends Record<string, unknown>>({
                                                                      getJsonData,
                                                                      getCsvData,
                                                                      filenamePrefix,
                                                                      columns,
                                                                      variant = 'outline',
                                                                      size = 'sm',
                                                                      disabled = false,
                                                                      className,
                                                                  }: DownloadButtonProps<TJson, TCsv>) {
    const [isDownloading, setIsDownloading] = useState(false);

    const handleDownload = async (format: ExportFormat) => {
        setIsDownloading(true);
        try {
            if (format === 'json') {
                const data = getJsonData();
                if (data.length > 0) downloadJson(data, filenamePrefix);
            } else {
                const data = getCsvData();
                if (data.length > 0) downloadCsv(data, filenamePrefix, columns);
            }
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
