import {NavLink, useLocation} from 'react-router-dom';
import type {LucideIcon} from 'lucide-react';
import {
    Activity,
    AlertTriangle,
    Bot,
    Brain,
    ChevronLeft,
    ChevronRight,
    Database,
    FileText,
    Globe,
    MessageSquare,
    Network,
    Radio,
    Search,
    Settings,
    Sparkles,
    Terminal,
} from 'lucide-react';
import {cn} from '@/lib/utils';
import {Button} from '@/components/ui/button';
import {Tooltip, TooltipContent, TooltipTrigger} from '@/components/ui/tooltip';
import {useDashboards} from '@/hooks/use-dashboards';

interface NavItem {
    to: string;
    icon: typeof Activity;
    label: string;
    shortcut: string;
}

const navItems: NavItem[] = [
    {to: '/', icon: Activity, label: 'RESOURCES', shortcut: 'R'},
    {to: '/traces', icon: Network, label: 'TRACES', shortcut: 'T'},
    {to: '/logs', icon: FileText, label: 'LOGS', shortcut: 'C'},
    {to: '/genai', icon: Sparkles, label: 'GENAI', shortcut: 'M'},
    {to: '/search', icon: Search, label: 'SEARCH', shortcut: '/'},
];

const aiNavItems: NavItem[] = [
    {to: '/agents', icon: Bot, label: 'AGENTS', shortcut: 'A'},
];

const dashboardIconMap: Record<string, LucideIcon> = {
    'activity': Activity,
    'globe': Globe,
    'brain': Brain,
    'database': Database,
    'alert-triangle': AlertTriangle,
    'message-square': MessageSquare,
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
                'flex flex-col bg-brutal-carbon border-r-3 border-brutal-zinc transition-all duration-200',
                collapsed ? 'w-16' : 'w-56'
            )}
        >
            {/* BRUTALIST Logo */}
            <div className="flex items-center h-14 px-4 border-b-3 border-brutal-zinc bg-brutal-dark">
                <NavLink to="/" className="flex items-center gap-2">
                    <div
                        className="w-8 h-8 bg-signal-orange flex items-center justify-center border-2 border-brutal-black">
                        <Terminal className="w-5 h-5 text-brutal-black"/>
                    </div>
                    {!collapsed && (
                        <div className="flex flex-col">
                            <span className="font-bold text-lg text-signal-orange tracking-wider">QYL.</span>
                            <span className="text-[10px] text-brutal-slate tracking-[0.3em]">OBSERVABILITY</span>
                        </div>
                    )}
                </NavLink>
            </div>

            {/* Connection Status */}
            {!collapsed && (
                <div className={cn(
                    'px-4 py-2 border-b-3 flex items-center gap-2 text-xs font-bold tracking-wider',
                    isLive
                        ? 'border-signal-green bg-signal-green/10 text-signal-green'
                        : 'border-brutal-zinc bg-brutal-dark text-brutal-slate'
                )}>
                    <Radio className={cn('w-3 h-3', isLive && 'animate-pulse-live')}/>
                    <span>{isLive ? 'CONNECTED' : 'DISCONNECTED'}</span>
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
                                'flex items-center gap-3 px-3 py-2 text-xs font-bold tracking-wider transition-all border-2',
                                isActive
                                    ? 'bg-signal-orange/20 text-signal-orange border-signal-orange'
                                    : 'text-brutal-slate border-transparent hover:border-brutal-zinc hover:bg-brutal-dark hover:text-brutal-white'
                            )}
                        >
                            <Icon className="w-5 h-5 flex-shrink-0"/>
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
                                    <kbd className="kbd text-[10px]">{item.shortcut}</kbd>
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
                    <div className="px-3 pt-2 pb-1 text-[10px] font-bold text-brutal-slate tracking-[0.3em] uppercase">
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
                                'flex items-center gap-3 px-3 py-2 text-xs font-bold tracking-wider transition-all border-2',
                                isActive
                                    ? 'bg-signal-orange/20 text-signal-orange border-signal-orange'
                                    : 'text-brutal-slate border-transparent hover:border-brutal-zinc hover:bg-brutal-dark hover:text-brutal-white'
                            )}
                        >
                            <Icon className="w-5 h-5 flex-shrink-0"/>
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
                                    <kbd className="kbd text-[10px]">{item.shortcut}</kbd>
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
                            className="px-3 pt-2 pb-1 text-[10px] font-bold text-brutal-slate tracking-[0.3em] uppercase">
                            DASHBOARDS
                        </div>
                    )}
                    {dashboards.map((db) => {
                        const Icon = dashboardIconMap[db.icon] ?? Activity;
                        const to = `/dashboards/${db.id}`;
                        const isActive = location.pathname === to;

                        const dbLinkContent = (
                            <NavLink
                                key={db.id}
                                to={to}
                                className={cn(
                                    'flex items-center gap-3 px-3 py-2 text-xs font-bold tracking-wider transition-all border-2',
                                    isActive
                                        ? 'bg-signal-orange/20 text-signal-orange border-signal-orange'
                                        : 'text-brutal-slate border-transparent hover:border-brutal-zinc hover:bg-brutal-dark hover:text-brutal-white'
                                )}
                            >
                                <Icon className="w-5 h-5 flex-shrink-0"/>
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
            <div className="border-t-3 border-brutal-zinc"/>

            {/* Bottom section */}
            <div className="p-2 space-y-1">
                {(() => {
                    const isActive = location.pathname === '/settings';

                    const settingsContent = (
                        <NavLink
                            to="/settings"
                            className={cn(
                                'flex items-center gap-3 px-3 py-2 text-xs font-bold tracking-wider transition-all border-2',
                                isActive
                                    ? 'bg-signal-orange/20 text-signal-orange border-signal-orange'
                                    : 'text-brutal-slate border-transparent hover:border-brutal-zinc hover:bg-brutal-dark hover:text-brutal-white'
                            )}
                        >
                            <Settings className="w-5 h-5 flex-shrink-0"/>
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
                                    <kbd className="kbd text-[10px]">,</kbd>
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
                    className="w-full justify-start text-xs font-bold tracking-wider text-brutal-slate hover:text-brutal-white hover:bg-brutal-dark border-2 border-transparent hover:border-brutal-zinc"
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

            {/* Footer */}
            {!collapsed && (
                <div className="px-4 py-2 border-t-3 border-brutal-zinc bg-brutal-dark">
                    <div className="text-[10px] text-brutal-slate tracking-[0.15em] leading-relaxed">
                        <div>OBSERVE EVERYTHING</div>
                        <div>JUDGE NOTHING</div>
                        <div>DOCUMENT PERFECTLY</div>
                    </div>
                </div>
            )}
        </aside>
    );
}
