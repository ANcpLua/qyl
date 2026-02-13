import {useState} from 'react';
import {AlertTriangle, Loader2, Trash2} from 'lucide-react';
import {Button} from '@/components/ui/button';
import {
    Dialog,
    DialogContent,
    DialogDescription,
    DialogFooter,
    DialogHeader,
    DialogTitle,
    DialogTrigger,
} from '@/components/ui/dialog';
import {toast} from 'sonner';

interface ClearTelemetryResponse {
    spansDeleted: number;
    logsDeleted: number;
    sessionsDeleted: number;
    consoleCleared: number;
    type: string;
}

interface ClearTelemetryDialogProps {
    onCleared?: () => void;
}

export function ClearTelemetryDialog({onCleared}: ClearTelemetryDialogProps) {
    const [open, setOpen] = useState(false);
    const [isClearing, setIsClearing] = useState(false);

    const handleClear = async () => {
        setIsClearing(true);
        try {
            const response = await fetch('/api/v1/telemetry', {
                method: 'DELETE',
                credentials: 'include',
            });

            if (!response.ok) {
                throw new Error(`Failed to clear telemetry: ${response.statusText}`);
            }

            const result: ClearTelemetryResponse = await response.json();

            const totalDeleted = result.spansDeleted + result.logsDeleted + result.sessionsDeleted;
            toast.success(`Cleared ${totalDeleted.toLocaleString()} records`, {
                description: `Spans: ${result.spansDeleted.toLocaleString()}, Logs: ${result.logsDeleted.toLocaleString()}, Sessions: ${result.sessionsDeleted.toLocaleString()}`,
            });

            setOpen(false);
            onCleared?.();
        } catch (error) {
            toast.error('Failed to clear telemetry', {
                description: error instanceof Error ? error.message : 'Unknown error',
            });
        } finally {
            setIsClearing(false);
        }
    };

    return (
        <Dialog open={open} onOpenChange={setOpen}>
            <DialogTrigger asChild>
                <Button
                    variant="outline"
                    size="icon"
                    className="border-2 border-brutal-zinc bg-brutal-dark text-brutal-slate hover:border-signal-red hover:text-signal-red hover:bg-signal-red/10"
                    aria-label="Clear all telemetry data"
                >
                    <Trash2 className="w-4 h-4"/>
                </Button>
            </DialogTrigger>
            <DialogContent>
                <DialogHeader>
                    <DialogTitle className="flex items-center gap-2">
                        <AlertTriangle className="w-5 h-5 text-signal-red"/>
                        CLEAR ALL TELEMETRY
                    </DialogTitle>
                    <DialogDescription>
                        This action cannot be undone. This will permanently delete all:
                    </DialogDescription>
                </DialogHeader>

                <div className="py-4 space-y-2">
                    <div className="flex items-center gap-2 text-brutal-white">
                        <div className="w-2 h-2 bg-signal-cyan"/>
                        <span className="text-sm font-mono">Spans and traces</span>
                    </div>
                    <div className="flex items-center gap-2 text-brutal-white">
                        <div className="w-2 h-2 bg-signal-green"/>
                        <span className="text-sm font-mono">Logs</span>
                    </div>
                    <div className="flex items-center gap-2 text-brutal-white">
                        <div className="w-2 h-2 bg-signal-orange"/>
                        <span className="text-sm font-mono">Sessions</span>
                    </div>
                    <div className="flex items-center gap-2 text-brutal-white">
                        <div className="w-2 h-2 bg-signal-yellow"/>
                        <span className="text-sm font-mono">Console logs</span>
                    </div>
                </div>

                <DialogFooter className="gap-2">
                    <Button
                        variant="outline"
                        onClick={() => setOpen(false)}
                        disabled={isClearing}
                        className="border-2 border-brutal-zinc bg-brutal-dark text-brutal-white hover:bg-brutal-zinc"
                    >
                        CANCEL
                    </Button>
                    <Button
                        variant="outline"
                        onClick={handleClear}
                        disabled={isClearing}
                        className="border-2 border-signal-red bg-signal-red/20 text-signal-red hover:bg-signal-red/40"
                    >
                        {isClearing ? (
                            <>
                                <Loader2 className="w-4 h-4 mr-2 animate-spin"/>
                                CLEARING...
                            </>
                        ) : (
                            <>
                                <Trash2 className="w-4 h-4 mr-2"/>
                                CLEAR ALL
                            </>
                        )}
                    </Button>
                </DialogFooter>
            </DialogContent>
        </Dialog>
    );
}
