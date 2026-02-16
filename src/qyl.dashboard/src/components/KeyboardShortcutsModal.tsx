import {Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle,} from '@/components/ui/dialog';

interface ShortcutItem {
    key: string;
    description: string;
    category: 'navigation' | 'actions';
}

const shortcuts: ShortcutItem[] = [
    // Navigation
    {key: 'R', description: 'Go to Resources', category: 'navigation'},
    {key: 'T', description: 'Go to Traces', category: 'navigation'},
    {key: 'C', description: 'Go to Console / Logs', category: 'navigation'},
    {key: 'S', description: 'Go to Structured logs', category: 'navigation'},
    {key: 'M', description: 'Go to Metrics / GenAI', category: 'navigation'},
    {key: ',', description: 'Open Settings', category: 'navigation'},
    // Actions
    {key: '?', description: 'Show keyboard shortcuts', category: 'actions'},
    {key: 'Ctrl + /', description: 'Focus search', category: 'actions'},
    {key: 'Esc', description: 'Close panel / Clear selection', category: 'actions'},
];

interface KeyboardShortcutsModalProps {
    open: boolean;
    onOpenChange: (open: boolean) => void;
}

export function KeyboardShortcutsModal({
                                           open,
                                           onOpenChange,
                                       }: KeyboardShortcutsModalProps) {
    const navigationShortcuts = shortcuts.filter((s) => s.category === 'navigation');
    const actionShortcuts = shortcuts.filter((s) => s.category === 'actions');

    return (
        <Dialog open={open} onOpenChange={onOpenChange}>
            <DialogContent className="max-w-md">
                <DialogHeader>
                    <DialogTitle>Keyboard Shortcuts</DialogTitle>
                    <DialogDescription>
                        Navigate the dashboard efficiently with keyboard shortcuts.
                    </DialogDescription>
                </DialogHeader>

                <div className="space-y-6">
                    {/* Navigation */}
                    <div>
                        <h3 className="section-header mb-3">Navigation</h3>
                        <div className="space-y-2">
                            {navigationShortcuts.map((shortcut) => (
                                <ShortcutRow
                                    key={shortcut.key}
                                    shortcutKey={shortcut.key}
                                    description={shortcut.description}
                                />
                            ))}
                        </div>
                    </div>

                    {/* Actions */}
                    <div>
                        <h3 className="section-header mb-3">Actions</h3>
                        <div className="space-y-2">
                            {actionShortcuts.map((shortcut) => (
                                <ShortcutRow
                                    key={shortcut.key}
                                    shortcutKey={shortcut.key}
                                    description={shortcut.description}
                                />
                            ))}
                        </div>
                    </div>
                </div>

                <div className="mt-4 pt-4 border-t-3 border-brutal-zinc">
                    <p className="text-[10px] text-brutal-slate tracking-wider">
                        Press <kbd className="kbd">Esc</kbd> or <kbd className="kbd">?</kbd> to close this modal
                    </p>
                </div>
            </DialogContent>
        </Dialog>
    );
}

function ShortcutRow({
                         shortcutKey,
                         description,
                     }: {
    shortcutKey: string;
    description: string;
}) {
    // Split compound keys (e.g., "Ctrl + /")
    const keys = shortcutKey.split(' + ');

    return (
        <div className="flex items-center justify-between py-1">
            <span className="text-sm text-brutal-white">{description}</span>
            <div className="flex items-center gap-1">
                {keys.map((key, index) => (
                    <span key={index} className="flex items-center gap-1">
                        <kbd className="kbd">{key}</kbd>
                        {index < keys.length - 1 && (
                            <span className="text-brutal-slate text-xs">+</span>
                        )}
                    </span>
                ))}
            </div>
        </div>
    );
}
