import * as React from 'react';
import {Dialog as DialogPrimitive} from '@base-ui/react/dialog';
import {X} from 'lucide-react';
import {cn} from '@/lib/utils';

const Dialog = DialogPrimitive.Root;

const DialogTrigger = DialogPrimitive.Trigger;

const DialogPortal = DialogPrimitive.Portal;

const DialogClose = DialogPrimitive.Close;

const DialogOverlay = React.forwardRef<
    HTMLDivElement,
    React.ComponentPropsWithoutRef<typeof DialogPrimitive.Backdrop>
>(({className, ...props}, ref) => (
    <DialogPrimitive.Backdrop
        ref={ref}
        className={cn(
            'fixed inset-0 z-50 bg-brutal-black/80 data-[open]:animate-in data-[closed]:animate-out data-[closed]:fade-out-0 data-[open]:fade-in-0',
            className
        )}
        {...props}
    />
));
DialogOverlay.displayName = 'DialogOverlay';

const DialogContent = React.forwardRef<
    HTMLDivElement,
    React.ComponentPropsWithoutRef<typeof DialogPrimitive.Popup>
>(({className, children, ...props}, ref) => (
    <DialogPortal>
        <DialogOverlay/>
        <DialogPrimitive.Popup
            ref={ref}
            className={cn(
                'fixed left-1/2 top-1/2 z-50 grid w-full max-w-lg -translate-x-1/2 -translate-y-1/2 gap-4 border-3 border-brutal-zinc bg-brutal-carbon p-6 shadow-brutal duration-200 data-[open]:animate-in data-[closed]:animate-out data-[closed]:fade-out-0 data-[open]:fade-in-0 data-[closed]:zoom-out-95 data-[open]:zoom-in-95',
                className
            )}
            {...props}
        >
            {children}
            <DialogPrimitive.Close
                className="absolute right-4 top-4 text-brutal-slate opacity-70 transition-opacity hover:opacity-100 hover:text-signal-orange focus-visible:outline-hidden disabled:pointer-events-none">
                <X className="h-4 w-4"/>
                <span className="sr-only">Close</span>
            </DialogPrimitive.Close>
        </DialogPrimitive.Popup>
    </DialogPortal>
));
DialogContent.displayName = 'DialogContent';

const DialogHeader = ({
                          className,
                          ...props
                      }: React.HTMLAttributes<HTMLDivElement>) => (
    <div
        className={cn(
            'flex flex-col space-y-1.5 text-center sm:text-left',
            className
        )}
        {...props}
    />
);
DialogHeader.displayName = 'DialogHeader';

const DialogFooter = ({
                          className,
                          ...props
                      }: React.HTMLAttributes<HTMLDivElement>) => (
    <div
        className={cn(
            'flex flex-col-reverse sm:flex-row sm:justify-end sm:space-x-2',
            className
        )}
        {...props}
    />
);
DialogFooter.displayName = 'DialogFooter';

const DialogTitle = React.forwardRef<
    HTMLHeadingElement,
    React.ComponentPropsWithoutRef<typeof DialogPrimitive.Title>
>(({className, ...props}, ref) => (
    <DialogPrimitive.Title
        ref={ref}
        className={cn(
            'text-lg font-bold leading-none tracking-wider text-signal-orange uppercase',
            className
        )}
        {...props}
    />
));
DialogTitle.displayName = 'DialogTitle';

const DialogDescription = React.forwardRef<
    HTMLParagraphElement,
    React.ComponentPropsWithoutRef<typeof DialogPrimitive.Description>
>(({className, ...props}, ref) => (
    <DialogPrimitive.Description
        ref={ref}
        className={cn('text-sm text-brutal-slate', className)}
        {...props}
    />
));
DialogDescription.displayName = 'DialogDescription';

export {
    Dialog,
    DialogPortal,
    DialogOverlay,
    DialogTrigger,
    DialogClose,
    DialogContent,
    DialogHeader,
    DialogFooter,
    DialogTitle,
    DialogDescription,
};
