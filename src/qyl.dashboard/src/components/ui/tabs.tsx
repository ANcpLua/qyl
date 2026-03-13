import {type ComponentPropsWithoutRef, forwardRef} from "react"
import {Tabs as TabsPrimitive} from "@base-ui/react/tabs"
import {cn} from "@/lib/utils"

const Tabs = TabsPrimitive.Root

const TabsList = forwardRef<
    HTMLDivElement,
    ComponentPropsWithoutRef<typeof TabsPrimitive.List>
>(({className, ...props}, ref) => (
    <TabsPrimitive.List
        ref={ref}
        className={cn(
            "inline-flex h-9 items-center justify-center rounded-none bg-muted p-1 text-muted-foreground",
            className
        )}
        {...props}
    />
))

TabsList.displayName = "TabsList"

const TabsTrigger = forwardRef<
    HTMLButtonElement,
    ComponentPropsWithoutRef<typeof TabsPrimitive.Tab>
>(({className, ...props}, ref) => (
    <TabsPrimitive.Tab
        ref={ref}
        className={cn(
            "inline-flex items-center justify-center whitespace-nowrap rounded-none px-3 py-1 text-sm font-medium ring-offset-background transition-colors focus-visible:outline-hidden focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 disabled:pointer-events-none disabled:opacity-50 data-[active]:bg-background data-[active]:text-foreground data-[active]:shadow-sm",
            className
        )}
        {...props}
    />
))

TabsTrigger.displayName = "TabsTrigger"

const TabsContent = forwardRef<
    HTMLDivElement,
    ComponentPropsWithoutRef<typeof TabsPrimitive.Panel>
>(({className, ...props}, ref) => (
    <TabsPrimitive.Panel
        ref={ref}
        className={cn(
            "mt-2 ring-offset-background focus-visible:outline-hidden focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2",
            className
        )}
        {...props}
    />
))

TabsContent.displayName = "TabsContent"

export {Tabs, TabsList, TabsTrigger, TabsContent}
