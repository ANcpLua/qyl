import {Monitor, Moon, Sun} from 'lucide-react';
import {Button} from '@/components/ui/button';
import {DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger,} from '@/components/ui/dropdown-menu';
import {useTheme} from '@/hooks/use-theme';
import {cn} from '@/lib/utils';

const themeOptions = [
    {value: 'light', label: 'LIGHT', icon: Sun},
    {value: 'dark', label: 'DARK', icon: Moon},
    {value: 'system', label: 'SYSTEM', icon: Monitor},
] as const;

export function ThemeToggle() {
    const {theme, setTheme, resolvedTheme} = useTheme();

    const CurrentIcon = resolvedTheme === 'dark' ? Moon : Sun;

    return (
        <DropdownMenu>
            <DropdownMenuTrigger asChild>
                <Button
                    variant="outline"
                    size="icon"
                    className="border-2 border-brutal-zinc bg-brutal-dark text-brutal-slate hover:border-signal-orange hover:text-signal-orange hover:bg-signal-orange/10"
                >
                    <CurrentIcon className="h-4 w-4"/>
                    <span className="sr-only">Toggle theme</span>
                </Button>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end">
                {themeOptions.map((option) => {
                    const Icon = option.icon;
                    const isActive = theme === option.value;
                    return (
                        <DropdownMenuItem
                            key={option.value}
                            onClick={() => setTheme(option.value)}
                            className={cn(
                                isActive && 'bg-brutal-dark text-signal-orange'
                            )}
                        >
                            <Icon className="mr-2 h-4 w-4"/>
                            {option.label}
                        </DropdownMenuItem>
                    );
                })}
            </DropdownMenuContent>
        </DropdownMenu>
    );
}
