import * as React from "react";
import {Check, Copy} from "lucide-react";
import {Button} from "./button";
import {Tooltip, TooltipContent, TooltipTrigger} from "./tooltip";
import {toast} from "sonner";
import {cn} from "@/lib/utils";

interface CopyButtonProps {
    value: string;
    className?: string;
    label?: string;
}

export function CopyButton({
                               value,
                               className,
                               label = "Value"
                           }: CopyButtonProps) {
    const [copied, setCopied] = React.useState(false);

    const handleCopy = async (e: React.MouseEvent) => {
        e.stopPropagation();

        try {
            await navigator.clipboard.writeText(value);
            setCopied(true);
            toast.success(`${label} copied to clipboard`);

            setTimeout(() => setCopied(false), 1500);
        } catch {
            toast.error("Failed to copy to clipboard");
        }
    };

    return (
        <Tooltip>
            <TooltipTrigger asChild>
                <Button
                    variant="ghost"
                    size="icon"
                    className={cn(
                        "h-6 w-6 opacity-0 group-hover:opacity-100 transition-opacity",
                        className
                    )}
                    onClick={handleCopy}
                    aria-label={copied ? "Copied!" : `Copy ${label.toLowerCase()}`}
                >
                    {copied ? (
                        <Check className="h-3 w-3 text-green-500"/>
                    ) : (
                        <Copy className="h-3 w-3"/>
                    )}
                </Button>
            </TooltipTrigger>
            <TooltipContent side="top">
                <p>{copied ? "Copied!" : `Copy ${label.toLowerCase()}`}</p>
            </TooltipContent>
        </Tooltip>
    );
}
