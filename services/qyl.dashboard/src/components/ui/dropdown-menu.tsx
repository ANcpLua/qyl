import * as React from "react"
import {Menu as MenuPrimitive} from "@base-ui/react/menu"
import {Check, ChevronRight, Circle} from "lucide-react"
import {cn} from "@/lib/utils"

const DropdownMenu = MenuPrimitive.Root

const DropdownMenuTrigger = MenuPrimitive.Trigger

const DropdownMenuGroup = MenuPrimitive.Group

const DropdownMenuPortal = MenuPrimitive.Portal

const DropdownMenuSub = MenuPrimitive.Root

const DropdownMenuRadioGroup = MenuPrimitive.RadioGroup

const DropdownMenuSubTrigger = React.forwardRef<
    HTMLDivElement,
    React.ComponentPropsWithoutRef<typeof MenuPrimitive.SubmenuTrigger> & {
    inset?: boolean
}
>(({className, inset, children, ...props}, ref) => (
    <MenuPrimitive.SubmenuTrigger
        ref={ref}
        className={cn(
            "flex cursor-default select-none items-center gap-2 px-2 py-1.5 text-xs font-bold tracking-wider outline-hidden focus-visible:outline-2 focus-visible:outline-offset-2 focus:bg-brutal-dark data-[open]:bg-brutal-dark [&_svg]:pointer-events-none [&_svg]:size-4 [&_svg]:shrink-0",
            inset && "pl-8",
            className
        )}
        {...props}
    >
        {children}
        <ChevronRight className="ml-auto"/>
    </MenuPrimitive.SubmenuTrigger>
))
DropdownMenuSubTrigger.displayName = "DropdownMenuSubTrigger"

const DropdownMenuSubContent = React.forwardRef<
    HTMLDivElement,
    React.ComponentPropsWithoutRef<typeof MenuPrimitive.Popup>
>(({className, ...props}, ref) => (
    <MenuPrimitive.Positioner>
        <MenuPrimitive.Popup
            ref={ref}
            className={cn(
                "z-50 min-w-[8rem] overflow-hidden border-2 border-brutal-zinc bg-brutal-carbon p-1 text-brutal-white shadow-brutal data-[open]:animate-in data-[closed]:animate-out data-[closed]:fade-out-0 data-[open]:fade-in-0 data-[closed]:zoom-out-95 data-[open]:zoom-in-95 data-[side=bottom]:slide-in-from-top-2 data-[side=left]:slide-in-from-right-2 data-[side=right]:slide-in-from-left-2 data-[side=top]:slide-in-from-bottom-2",
                className
            )}
            {...props}
        />
    </MenuPrimitive.Positioner>
))
DropdownMenuSubContent.displayName = "DropdownMenuSubContent"

const DropdownMenuContent = React.forwardRef<
    HTMLDivElement,
    React.ComponentPropsWithoutRef<typeof MenuPrimitive.Popup> & {
    sideOffset?: number
    align?: "start" | "center" | "end"
}
>(({className, sideOffset = 4, align, ...props}, ref) => (
    <MenuPrimitive.Portal>
        <MenuPrimitive.Positioner sideOffset={sideOffset} align={align}>
            <MenuPrimitive.Popup
                ref={ref}
                className={cn(
                    "z-50 min-w-[8rem] overflow-hidden border-2 border-brutal-zinc bg-brutal-carbon p-1 text-brutal-white shadow-brutal",
                    "data-[open]:animate-in data-[closed]:animate-out data-[closed]:fade-out-0 data-[open]:fade-in-0 data-[closed]:zoom-out-95 data-[open]:zoom-in-95 data-[side=bottom]:slide-in-from-top-2 data-[side=left]:slide-in-from-right-2 data-[side=right]:slide-in-from-left-2 data-[side=top]:slide-in-from-bottom-2",
                    className
                )}
                {...props}
            />
        </MenuPrimitive.Positioner>
    </MenuPrimitive.Portal>
))
DropdownMenuContent.displayName = "DropdownMenuContent"

const DropdownMenuItem = React.forwardRef<
    HTMLDivElement,
    React.ComponentPropsWithoutRef<typeof MenuPrimitive.Item> & {
    inset?: boolean
}
>(({className, inset, ...props}, ref) => (
    <MenuPrimitive.Item
        ref={ref}
        className={cn(
            "relative flex cursor-default select-none items-center gap-2 px-2 py-1.5 text-xs font-bold tracking-wider outline-hidden focus-visible:outline-2 focus-visible:outline-offset-2 transition-colors data-[highlighted]:bg-brutal-dark data-[highlighted]:text-signal-orange data-[disabled]:pointer-events-none data-[disabled]:opacity-50 [&>svg]:size-4 [&>svg]:shrink-0",
            inset && "pl-8",
            className
        )}
        {...props}
    />
))
DropdownMenuItem.displayName = "DropdownMenuItem"

const DropdownMenuCheckboxItem = React.forwardRef<
    HTMLDivElement,
    React.ComponentPropsWithoutRef<typeof MenuPrimitive.CheckboxItem>
>(({className, children, ...props}, ref) => (
    <MenuPrimitive.CheckboxItem
        ref={ref}
        className={cn(
            "relative flex cursor-default select-none items-center py-1.5 pl-8 pr-2 text-xs font-bold tracking-wider outline-hidden focus-visible:outline-2 focus-visible:outline-offset-2 transition-colors data-[highlighted]:bg-brutal-dark data-[highlighted]:text-signal-orange data-[disabled]:pointer-events-none data-[disabled]:opacity-50",
            className
        )}
        {...props}
    >
    <span className="absolute left-2 flex h-3.5 w-3.5 items-center justify-center">
      <MenuPrimitive.CheckboxItemIndicator>
        <Check className="h-4 w-4"/>
      </MenuPrimitive.CheckboxItemIndicator>
    </span>
        {children}
    </MenuPrimitive.CheckboxItem>
))
DropdownMenuCheckboxItem.displayName = "DropdownMenuCheckboxItem"

const DropdownMenuRadioItem = React.forwardRef<
    HTMLDivElement,
    React.ComponentPropsWithoutRef<typeof MenuPrimitive.RadioItem>
>(({className, children, ...props}, ref) => (
    <MenuPrimitive.RadioItem
        ref={ref}
        className={cn(
            "relative flex cursor-default select-none items-center py-1.5 pl-8 pr-2 text-xs font-bold tracking-wider outline-hidden focus-visible:outline-2 focus-visible:outline-offset-2 transition-colors data-[highlighted]:bg-brutal-dark data-[highlighted]:text-signal-orange data-[disabled]:pointer-events-none data-[disabled]:opacity-50",
            className
        )}
        {...props}
    >
    <span className="absolute left-2 flex h-3.5 w-3.5 items-center justify-center">
      <MenuPrimitive.RadioItemIndicator>
        <Circle className="h-2 w-2 fill-current"/>
      </MenuPrimitive.RadioItemIndicator>
    </span>
        {children}
    </MenuPrimitive.RadioItem>
))
DropdownMenuRadioItem.displayName = "DropdownMenuRadioItem"

const DropdownMenuLabel = React.forwardRef<
    HTMLDivElement,
    React.ComponentPropsWithoutRef<typeof MenuPrimitive.GroupLabel> & {
    inset?: boolean
}
>(({className, inset, ...props}, ref) => (
    <MenuPrimitive.GroupLabel
        ref={ref}
        className={cn(
            "px-2 py-1.5 text-xs font-bold tracking-wider text-brutal-slate",
            inset && "pl-8",
            className
        )}
        {...props}
    />
))
DropdownMenuLabel.displayName = "DropdownMenuLabel"

const DropdownMenuSeparator = React.forwardRef<
    HTMLDivElement,
    React.ComponentPropsWithoutRef<typeof MenuPrimitive.Separator>
>(({className, ...props}, ref) => (
    <MenuPrimitive.Separator
        ref={ref}
        className={cn("-mx-1 my-1 h-px bg-brutal-zinc", className)}
        {...props}
    />
))
DropdownMenuSeparator.displayName = "DropdownMenuSeparator"

const DropdownMenuShortcut = ({
                                  className,
                                  ...props
                              }: React.HTMLAttributes<HTMLSpanElement>) => {
    return (
        <span
            className={cn("ml-auto text-xs tracking-widest text-brutal-slate", className)}
            {...props}
        />
    )
}
DropdownMenuShortcut.displayName = "DropdownMenuShortcut"

export {
    DropdownMenu,
    DropdownMenuTrigger,
    DropdownMenuContent,
    DropdownMenuItem,
    DropdownMenuCheckboxItem,
    DropdownMenuRadioItem,
    DropdownMenuLabel,
    DropdownMenuSeparator,
    DropdownMenuShortcut,
    DropdownMenuGroup,
    DropdownMenuPortal,
    DropdownMenuSub,
    DropdownMenuSubContent,
    DropdownMenuSubTrigger,
    DropdownMenuRadioGroup,
}
