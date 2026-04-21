import {CopyButton} from "./copy-button";
import {cn} from "@/lib/utils";

interface CopyableTextProps {
    value: string;
    label?: string;
    className?: string;
    textClassName?: string;
    truncate?: boolean;
    maxWidth?: string;
}

export function CopyableText({
                                 value,
                                 label,
                                 className,
                                 textClassName,
                                 truncate = false,
                                 maxWidth = "200px"
                             }: CopyableTextProps) {
    return (
        <div className={cn("group inline-flex items-center gap-1", className)}>
      <span
          className={cn(
              "font-mono text-sm",
              truncate && "truncate",
              textClassName
          )}
          style={truncate ? {maxWidth} : undefined}
          title={truncate ? value : undefined}
      >
        {value}
      </span>
            <CopyButton value={value} label={label}/>
        </div>
    );
}
