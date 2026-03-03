import {Bot} from 'lucide-react';
import {Button} from '@/components/ui/button';
import {cn} from '@/lib/utils';

interface CopilotButtonProps {
    onClick: () => void;
    isOpen: boolean;
    isStreaming?: boolean;
    username?: string;
}

export function CopilotButton({onClick, isOpen, isStreaming, username}: CopilotButtonProps) {
    return (
        <Button
            onClick={onClick}
            className={cn(
                'fixed bottom-4 right-3 sm:right-4 z-50 h-11 gap-2 border-2 font-semibold tracking-[0.08em] text-xs shadow-[0_14px_28px_-14px_rgba(0,0,0,0.8)] transition-colors',
                isOpen
                    ? 'bg-signal-violet border-signal-violet text-brutal-white hover:bg-signal-violet/90'
                    : 'bg-brutal-carbon/95 border-brutal-zinc text-signal-violet hover:bg-signal-violet/10 hover:border-signal-violet',
                isStreaming && 'animate-pulse',
            )}
        >
            <Bot className="w-4 h-4"/>
            <span>COPILOT</span>
            {username && (
                <span className="text-[10px] opacity-60">@{username}</span>
            )}
        </Button>
    );
}
