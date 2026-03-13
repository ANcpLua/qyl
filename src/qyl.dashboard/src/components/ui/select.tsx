import {type ComponentPropsWithoutRef, type ReactNode, forwardRef} from "react"
import {Select as SelectPrimitive} from "@base-ui/react/select"
import {Check, ChevronDown, ChevronUp} from "lucide-react"
import {cn} from "@/lib/utils"

interface SelectProps {
    children?: ReactNode
    value?: string
    defaultValue?: string
    onValueChange?: (value: string) => void
    open?: boolean
    onOpenChange?: (open: boolean) => void
    disabled?: boolean
    name?: string
    required?: boolean
}

function Select({onValueChange, ...props}: SelectProps) {
    return (
        <SelectPrimitive.Root
            {...props}
            onValueChange={onValueChange
                ? (value) => { if (value !== null) onValueChange(value) }
                : undefined}
        />
    )
}
const SelectGroup = SelectPrimitive.Group
const SelectValue = SelectPrimitive.Value

const SelectTrigger = forwardRef<
    HTMLButtonElement,
    ComponentPropsWithoutRef<typeof SelectPrimitive.Trigger>
>(({className, children, ...props}, ref) => (
    <SelectPrimitive.Trigger
        ref={ref}
        className={cn(
            "flex h-9 w-full items-center justify-between whitespace-nowrap rounded-none border border-input bg-transparent px-3 py-2 text-sm shadow-xs ring-offset-background placeholder:text-muted-foreground focus:outline-hidden focus:ring-1 focus:ring-ring disabled:cursor-not-allowed disabled:opacity-50 [&>span]:line-clamp-1",
            className
        )}
        {...props}
    >
        {children}
        <SelectPrimitive.Icon>
            <ChevronDown className="h-4 w-4 opacity-50"/>
        </SelectPrimitive.Icon>
    </SelectPrimitive.Trigger>
))
SelectTrigger.displayName = "SelectTrigger"

const SelectScrollUpButton = forwardRef<
    HTMLDivElement,
    ComponentPropsWithoutRef<typeof SelectPrimitive.ScrollUpArrow>
>(({className, ...props}, ref) => (
    <SelectPrimitive.ScrollUpArrow
        ref={ref}
        className={cn(
            "flex cursor-default items-center justify-center py-1",
            className
        )}
        {...props}
    >
        <ChevronUp className="h-4 w-4"/>
    </SelectPrimitive.ScrollUpArrow>
))
SelectScrollUpButton.displayName = "SelectScrollUpButton"

const SelectScrollDownButton = forwardRef<
    HTMLDivElement,
    ComponentPropsWithoutRef<typeof SelectPrimitive.ScrollDownArrow>
>(({className, ...props}, ref) => (
    <SelectPrimitive.ScrollDownArrow
        ref={ref}
        className={cn(
            "flex cursor-default items-center justify-center py-1",
            className
        )}
        {...props}
    >
        <ChevronDown className="h-4 w-4"/>
    </SelectPrimitive.ScrollDownArrow>
))
SelectScrollDownButton.displayName = "SelectScrollDownButton"

const SelectContent = forwardRef<
    HTMLDivElement,
    ComponentPropsWithoutRef<typeof SelectPrimitive.Popup>
>(({className, children, ...props}, ref) => (
    <SelectPrimitive.Portal>
        <SelectPrimitive.Positioner>
            <SelectPrimitive.Popup
                ref={ref}
                className={cn(
                    "relative z-50 max-h-96 min-w-[8rem] overflow-hidden rounded-none border bg-popover text-popover-foreground shadow-md data-[open]:animate-in data-[closed]:animate-out data-[closed]:fade-out-0 data-[open]:fade-in-0 data-[closed]:zoom-out-95 data-[open]:zoom-in-95 data-[side=bottom]:slide-in-from-top-2 data-[side=left]:slide-in-from-right-2 data-[side=right]:slide-in-from-left-2 data-[side=top]:slide-in-from-bottom-2",
                    className
                )}
                {...props}
            >
                <SelectScrollUpButton/>
                {children}
                <SelectScrollDownButton/>
            </SelectPrimitive.Popup>
        </SelectPrimitive.Positioner>
    </SelectPrimitive.Portal>
))
SelectContent.displayName = "SelectContent"

const SelectLabel = forwardRef<
    HTMLDivElement,
    ComponentPropsWithoutRef<typeof SelectPrimitive.GroupLabel>
>(({className, ...props}, ref) => (
    <SelectPrimitive.GroupLabel
        ref={ref}
        className={cn("px-2 py-1.5 text-sm font-semibold", className)}
        {...props}
    />
))
SelectLabel.displayName = "SelectLabel"

const SelectItem = forwardRef<
    HTMLDivElement,
    ComponentPropsWithoutRef<typeof SelectPrimitive.Item>
>(({className, children, ...props}, ref) => (
    <SelectPrimitive.Item
        ref={ref}
        className={cn(
            "relative flex w-full cursor-default select-none items-center rounded-xs py-1.5 pl-2 pr-8 text-sm outline-hidden data-[highlighted]:bg-accent data-[highlighted]:text-accent-foreground data-[disabled]:pointer-events-none data-[disabled]:opacity-50",
            className
        )}
        {...props}
    >
        <span className="absolute right-2 flex h-3.5 w-3.5 items-center justify-center">
            <SelectPrimitive.ItemIndicator>
                <Check className="h-4 w-4"/>
            </SelectPrimitive.ItemIndicator>
        </span>
        <SelectPrimitive.ItemText>{children}</SelectPrimitive.ItemText>
    </SelectPrimitive.Item>
))
SelectItem.displayName = "SelectItem"

const SelectSeparator = forwardRef<
    HTMLDivElement,
    ComponentPropsWithoutRef<typeof SelectPrimitive.Separator>
>(({className, ...props}, ref) => (
    <SelectPrimitive.Separator
        ref={ref}
        className={cn("-mx-1 my-1 h-px bg-muted", className)}
        {...props}
    />
))
SelectSeparator.displayName = "SelectSeparator"

export {
    Select,
    SelectGroup,
    SelectValue,
    SelectTrigger,
    SelectContent,
    SelectLabel,
    SelectItem,
    SelectSeparator,
    SelectScrollUpButton,
    SelectScrollDownButton,
}
