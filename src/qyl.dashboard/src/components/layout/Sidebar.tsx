import {NavLink, useLocation} from 'react-router-dom';
import type {Icon as PhosphorIcon} from '@phosphor-icons/react';
import {
    Broadcast,
    CaretLeft,
    CaretRight,
    ChartBar,
    ChatDots,
    Database,
    Eye,
    FileText,
    Globe,
    Lightning,
    MagnifyingGlass,
    Pulse,
    Robot,
    Sparkle,
    Terminal,
    TreeStructure,
    Warning,
    GearSix,
} from '@phosphor-icons/react';
import {cn} from '@/lib/utils';
import {Button} from '@/components/ui/button';
import {Tooltip, TooltipContent, TooltipTrigger} from '@/components/ui/tooltip';
import {useDashboards} from '@/hooks/use-dashboards';

interface NavItem {
    to: string;
    icon: PhosphorIcon;
    label: string;
    shortcut: string;
}

const navItems: NavItem[] = [
    {to: '/', icon: Pulse, label: 'RESOURCES', shortcut: 'R'},
    {to: '/traces', icon: TreeStructure, label: 'TRACES', shortcut: 'T'},
    {to: '/logs', icon: FileText, label: 'LOGS', shortcut: 'C'},
    {to: '/genai', icon: Sparkle, label: 'GENAI', shortcut: 'M'},
    {to: '/search', icon: MagnifyingGlass, label: 'SEARCH', shortcut: '/'},
];

const aiNavItems: NavItem[] = [
    {to: '/agents', icon: Robot, label: 'AGENTS', shortcut: 'A'},
    {to: '/bot', icon: ChartBar, label: 'BOT', shortcut: 'B'},
    {to: '/Loom', icon: Eye, label: 'Loom', shortcut: 'S'},
];

const dashboardIconMap: Record<string, PhosphorIcon> = {
    'activity': Pulse,
    'globe': Globe,
    'brain': Lightning,
    'database': Database,
    'alert-triangle': Warning,
    'message-square': ChatDots,
};

interface SidebarProps {
    collapsed: boolean;
    onCollapsedChange: (collapsed: boolean) => void;
    isLive: boolean;
}

export function Sidebar({collapsed, onCollapsedChange, isLive}: SidebarProps) {
    const location = useLocation();
    const {data: dashboards} = useDashboards();

    return (
        <aside
            className={cn(
                'relative flex flex-col bg-brutal-carbon/92 border-r border-brutal-zinc/70 transition-[width] duration-200',
                collapsed ? 'w-14' : 'w-52'
            )}
        >
            {/* BRUTALIST Logo */}
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

            {/* Connection Status */}
            {!collapsed && (
                <div className={cn(
                    'px-3 py-1.5 border-b flex items-center gap-1.5 text-[11px] font-semibold tracking-[0.08em]',
                    isLive
                        ? 'border-signal-green bg-signal-green/10 text-signal-green'
                        : 'border-brutal-zinc/70 bg-brutal-dark/80 text-brutal-slate'
                )}>
                    <Broadcast className={cn('w-3 h-3', isLive && 'animate-pulse-live')}/>
                    <span>{isLive ? 'STREAMING LIVE' : 'STREAM OFFLINE'}</span>
                    {isLive && <span className="animate-cursor-blink">_</span>}
                </div>
            )}

            {/* Navigation */}
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
                            <Tooltip key={item.to} delayDuration={0}>
                                <TooltipTrigger asChild>{linkContent}</TooltipTrigger>
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

            {/* AI Section */}
            <div className="px-2 space-y-1">
                {!collapsed && (
                    <div className="px-3 pt-2 pb-1 text-[11px] font-bold text-brutal-slate tracking-[0.3em] uppercase">
                        AI
                    </div>
                )}
                {aiNavItems.map((item) => {
                    const isActive = location.pathname === item.to || location.pathname.startsWith(item.to + '/');
                    const Icon = item.icon;

                    const aiLinkContent = (
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
                            <Tooltip key={item.to} delayDuration={0}>
                                <TooltipTrigger asChild>{aiLinkContent}</TooltipTrigger>
                                <TooltipContent side="right"
                                                className="flex items-center gap-2 bg-brutal-carbon border-2 border-brutal-zinc">
                                    {item.label}
                                    <kbd className="kbd text-[11px]">{item.shortcut}</kbd>
                                </TooltipContent>
                            </Tooltip>
                        );
                    }

                    return aiLinkContent;
                })}
            </div>

            {/* Auto-detected dashboards */}
            {dashboards && dashboards.length > 0 && (
                <div className="px-2 space-y-1">
                    {!collapsed && (
                        <div
                            className="px-3 pt-2 pb-1 text-[11px] font-bold text-brutal-slate tracking-[0.3em] uppercase">
                            DASHBOARDS
                        </div>
                    )}
                    {dashboards.map((db) => {
                        const Icon = dashboardIconMap[db.icon] ?? Pulse;
                        const to = `/dashboards/${db.id}`;
                        const isActive = location.pathname === to;

                        const dbLinkContent = (
                            <NavLink
                                key={db.id}
                                to={to}
                                className={cn(
                                    'flex items-center gap-2.5 px-2.5 py-1.5 text-[11px] font-semibold tracking-[0.08em] transition-colors border',
                                    isActive
                                        ? 'bg-signal-orange/14 text-signal-orange border-signal-orange/55 shadow-[inset_0_0_0_1px_rgba(0,0,0,0.35)]'
                                        : 'text-brutal-slate border-transparent hover:border-brutal-zinc/70 hover:bg-brutal-dark/80 hover:text-brutal-white'
                                )}
                            >
                                <Icon className="w-4 h-4 flex-shrink-0"/>
                                {!collapsed && (
                                    <span className="flex-1 truncate">{db.title.toUpperCase()}</span>
                                )}
                            </NavLink>
                        );

                        if (collapsed) {
                            return (
                                <Tooltip key={db.id} delayDuration={0}>
                                    <TooltipTrigger asChild>{dbLinkContent}</TooltipTrigger>
                                    <TooltipContent side="right"
                                                    className="flex items-center gap-2 bg-brutal-carbon border-2 border-brutal-zinc">
                                        {db.title.toUpperCase()}
                                    </TooltipContent>
                                </Tooltip>
                            );
                        }

                        return dbLinkContent;
                    })}
                </div>
            )}

            {/* Separator line */}
            <div className="border-t border-brutal-zinc/70"/>

            {/* Bottom section */}
            <div className="p-2 space-y-1">
                {(() => {
                    const isActive = location.pathname === '/settings';

                    const settingsContent = (
                        <NavLink
                            to="/settings"
                            className={cn(
                                'flex items-center gap-2.5 px-2.5 py-1.5 text-[11px] font-semibold tracking-[0.08em] transition-colors border',
                                isActive
                                    ? 'bg-signal-orange/14 text-signal-orange border-signal-orange/55 shadow-[inset_0_0_0_1px_rgba(0,0,0,0.35)]'
                                    : 'text-brutal-slate border-transparent hover:border-brutal-zinc/70 hover:bg-brutal-dark/80 hover:text-brutal-white'
                            )}
                        >
                            <GearSix className="w-4 h-4 flex-shrink-0"/>
                            {!collapsed && (
                                <>
                                    <span className="flex-1">SETTINGS</span>
                                    <kbd className="kbd text-[10px]">,</kbd>
                                </>
                            )}
                        </NavLink>
                    );

                    if (collapsed) {
                        return (
                            <Tooltip delayDuration={0}>
                                <TooltipTrigger asChild>{settingsContent}</TooltipTrigger>
                                <TooltipContent side="right"
                                                className="flex items-center gap-2 bg-brutal-carbon border-2 border-brutal-zinc">
                                    SETTINGS
                                    <kbd className="kbd text-[11px]">,</kbd>
                                </TooltipContent>
                            </Tooltip>
                        );
                    }

                    return settingsContent;
                })()}

                {/* Collapse toggle */}
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
                        <CaretRight className="w-5 h-5"/>
                    ) : (
                        <>
                            <CaretLeft className="w-5 h-5"/>
                            <span className="ml-3">COLLAPSE</span>
                        </>
                    )}
                </Button>
            </div>

            {/* Footer */}
            {!collapsed && (
                <div className="px-4 py-2 border-t border-brutal-zinc/70 bg-brutal-dark/80">
                    <div className="text-[11px] text-brutal-slate tracking-[0.15em] leading-relaxed">
                        <div>QUESTION YOUR LOGS</div>
                    </div>
                </div>
            )}
        </aside>
    );
}
