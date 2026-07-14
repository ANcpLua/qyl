import {NavLink, useLocation} from 'react-router-dom';
import type {LucideIcon} from 'lucide-react';
import {
    ChevronLeft,
    ChevronRight,
    CircleDollarSign,
    FileText,
    GitBranch,
    Terminal,
} from 'lucide-react';
import {cn} from '@/lib/utils';
import {Button} from '@/components/ui/button';
import {Tooltip, TooltipContent, TooltipProvider, TooltipTrigger} from '@/components/ui/tooltip';

interface NavItem {
    to: string;
    icon: LucideIcon;
    label: string;
    shortcut: string;
}

const navItems: NavItem[] = [
    {to: '/traces', icon: GitBranch, label: 'TRACES', shortcut: 'T'},
    {to: '/logs', icon: FileText, label: 'LOGS', shortcut: 'C'},
    {to: '/cost', icon: CircleDollarSign, label: 'COST', shortcut: '$'},
];

interface SidebarProps {
    collapsed: boolean;
    onCollapsedChange: (collapsed: boolean) => void;
}

export function Sidebar({collapsed, onCollapsedChange}: SidebarProps) {
    const location = useLocation();

    return (
        <TooltipProvider delay={0}>
            <aside
                className={cn(
                    'relative flex flex-col bg-brutal-carbon/92 border-r border-brutal-zinc/70 transition-[width] duration-200',
                    collapsed ? 'w-14' : 'w-52'
                )}
            >
                <div className="flex items-center h-12 px-3 border-b border-brutal-zinc/70 bg-brutal-dark/90">
                    <NavLink to="/" className="flex items-center gap-1.5">
                        <div
                            className="w-7 h-7 bg-signal-orange flex items-center justify-center border border-brutal-black">
                            <Terminal className="w-4 h-4 text-brutal-black"/>
                        </div>
                        {!collapsed && (
                            <div className="flex flex-col">
                                <span className="font-bold text-base text-signal-orange tracking-wider">QYL.</span>
                                <span className="text-[11px] text-brutal-slate tracking-[0.3em]">OBSERVABILITY</span>
                            </div>
                        )}
                    </NavLink>
                </div>

                <nav className="flex-1 p-2 space-y-1" aria-label="Main navigation">
                    {navItems.map((item) => {
                        const isActive = location.pathname === item.to;
                        const Icon = item.icon;

                        const linkContent = (
                            <NavLink
                                key={item.to}
                                to={item.to}
                                className={cn(
                                    'flex items-center gap-2.5 px-2.5 py-1.5 text-[11px] font-semibold tracking-[0.08em] transition-colors border',
                                    isActive
                                        ? 'bg-signal-orange/14 text-signal-orange border-signal-orange/55 shadow-[inset_0_0_0_1px_rgba(0,0,0,0.35)]'
                                        : 'text-brutal-slate border-transparent hover:border-brutal-zinc/70 hover:bg-brutal-dark/80 hover:text-brutal-white'
                                )}
                            >
                                <Icon className="w-4 h-4 flex-shrink-0"/>
                                {!collapsed && (
                                    <>
                                        <span className="flex-1">{item.label}</span>
                                        <kbd className="kbd text-[10px]">{item.shortcut}</kbd>
                                    </>
                                )}
                            </NavLink>
                        );

                        if (collapsed) {
                            return (
                                <Tooltip key={item.to}>
                                    <TooltipTrigger render={linkContent}/>
                                    <TooltipContent side="right"
                                                    className="flex items-center gap-2 bg-brutal-carbon border-2 border-brutal-zinc">
                                        {item.label}
                                        <kbd className="kbd text-[11px]">{item.shortcut}</kbd>
                                    </TooltipContent>
                                </Tooltip>
                            );
                        }

                        return linkContent;
                    })}
                </nav>

                <div className="border-t border-brutal-zinc/70"/>

                <div className="p-2 space-y-1">
                    <Button
                        variant="ghost"
                        size="sm"
                        aria-pressed={collapsed}
                        className={cn(
                            'w-full justify-start text-xs font-semibold tracking-[0.08em] border',
                            collapsed
                                ? 'text-signal-orange border-signal-orange/45 bg-signal-orange/10 hover:bg-signal-orange/20'
                                : 'text-brutal-slate border-transparent hover:text-brutal-white hover:bg-brutal-dark hover:border-brutal-zinc/70'
                        )}
                        onClick={() => onCollapsedChange(!collapsed)}
                    >
                        {collapsed ? (
                            <ChevronRight className="w-5 h-5"/>
                        ) : (
                            <>
                                <ChevronLeft className="w-5 h-5"/>
                                <span className="ml-3">COLLAPSE</span>
                            </>
                        )}
                    </Button>
                </div>

                {!collapsed && (
                    <div className="px-4 py-2 border-t border-brutal-zinc/70 bg-brutal-dark/80">
                        <div className="text-[11px] text-brutal-slate tracking-[0.15em] leading-relaxed">
                            <div>QUESTION YOUR LOGS</div>
                        </div>
                    </div>
                )}
            </aside>
        </TooltipProvider>
    );
}
