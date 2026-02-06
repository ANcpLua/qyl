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
                'fixed bottom-4 right-4 z-50 h-12 gap-2 border-3 font-bold tracking-[0.1em] text-xs shadow-[3px_3px_0_0_rgba(0,0,0,0.5)] transition-all',
                isOpen
                    ? 'bg-signal-purple border-signal-purple text-brutal-white hover:bg-signal-purple/90'
                    : 'bg-brutal-carbon border-signal-purple text-signal-purple hover:bg-signal-purple/10',
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
