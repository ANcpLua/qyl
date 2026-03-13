import {type ButtonHTMLAttributes, cloneElement, forwardRef, isValidElement, type ReactElement} from "react"
import {cva, type VariantProps} from "class-variance-authority"
import {cn} from "@/lib/utils"

const buttonVariants = cva(
    "inline-flex items-center justify-center gap-2 whitespace-nowrap rounded-none text-sm font-medium transition-colors focus-visible:outline-hidden focus-visible:ring-1 focus-visible:ring-ring disabled:pointer-events-none disabled:opacity-50 [&_svg]:pointer-events-none [&_svg]:size-4 [&_svg]:shrink-0",
    {
        variants: {
            variant: {
                default:
                    "bg-primary text-primary-foreground shadow hover:bg-primary/90",
                destructive:
                    "bg-destructive text-destructive-foreground shadow-xs hover:bg-destructive/90",
                outline:
                    "border border-input bg-background shadow-xs hover:bg-accent hover:text-accent-foreground",
                secondary:
                    "bg-secondary text-secondary-foreground shadow-xs hover:bg-secondary/80",
                ghost: "hover:bg-accent hover:text-accent-foreground",
                link: "text-primary underline-offset-4 hover:underline",
            },
            size: {
                default: "h-9 px-4 py-2",
                sm: "h-8 rounded-none px-3 text-xs",
                lg: "h-10 rounded-none px-8",
                icon: "h-9 w-9",
            },
        },
        defaultVariants: {
            variant: "default",
            size: "default",
        },
    }
)

export interface ButtonProps
    extends ButtonHTMLAttributes<HTMLButtonElement>,
        VariantProps<typeof buttonVariants> {
    /** Render a different element with button styling. Pass a ReactElement to replace the default <button>. */
    render?: ReactElement<Record<string, unknown>>
}

export const Button = forwardRef<HTMLButtonElement, ButtonProps>(
    ({className, variant, size, render, children, ...props}, ref) => {
        const classes = cn(buttonVariants({variant, size, className}))

        if (render && isValidElement(render)) {
            return cloneElement(render, {
                ...props,
                ref,
                className: cn(classes, (render.props as Record<string, unknown>).className as string | undefined),
            } as Record<string, unknown>, children)
        }

        return (
            <button
                className={classes}
                ref={ref}
                {...props}
            >
                {children}
            </button>
        )
    }
)

Button.displayName = "Button"

export {buttonVariants}
