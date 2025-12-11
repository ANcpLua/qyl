import {NavLink, useLocation} from 'react-router-dom';
import {
  Activity,
  BarChart3,
  ChevronLeft,
  ChevronRight,
  FileText,
  Network,
  Radio,
  Settings,
  Sparkles,
} from 'lucide-react';
import {cn} from '@/lib/utils';
import {Button} from '@/components/ui/button';
import {Separator} from '@/components/ui/separator';
import {Tooltip, TooltipContent, TooltipTrigger,} from '@/components/ui/tooltip';

interface NavItem {
  to: string;
  icon: typeof Activity;
  label: string;
  shortcut: string;
}

const navItems: NavItem[] = [
  {to: '/', icon: Activity, label: 'Resources', shortcut: 'G'},
  {to: '/traces', icon: Network, label: 'Traces', shortcut: 'T'},
  {to: '/logs', icon: FileText, label: 'Logs', shortcut: 'L'},
  {to: '/metrics', icon: BarChart3, label: 'Metrics', shortcut: 'M'},
  {to: '/genai', icon: Sparkles, label: 'GenAI', shortcut: 'A'},
];

interface SidebarProps {
  collapsed: boolean;
  onCollapsedChange: (collapsed: boolean) => void;
  isLive: boolean;
}

export function Sidebar({collapsed, onCollapsedChange, isLive}: SidebarProps) {
  const location = useLocation();

  return (
    <aside
      className={cn(
        'flex flex-col bg-card border-r border-border transition-all duration-200',
        collapsed ? 'w-16' : 'w-56'
      )}
    >
      {/* Logo */}
      <div className="flex items-center h-14 px-4 border-b border-border">
        <NavLink to="/" className="flex items-center gap-2">
          <div
            className="w-8 h-8 rounded-lg bg-gradient-to-br from-cyan-500 to-violet-500 flex items-center justify-center">
            <span className="text-white font-bold text-sm">Q</span>
          </div>
          {!collapsed && (
            <span className="font-semibold text-lg text-gradient">qyl.</span>
          )}
        </NavLink>
        {!collapsed && isLive && (
          <div className="ml-auto flex items-center gap-1.5">
            <Radio className="w-3 h-3 text-green-500 pulse-live"/>
            <span className="text-xs text-green-500">Live</span>
          </div>
        )}
      </div>

      {/* Navigation */}
      <nav className="flex-1 p-2 space-y-1">
        {navItems.map((item) => {
          const isActive = location.pathname === item.to;
          const Icon = item.icon;

          const linkContent = (
            <NavLink
              key={item.to}
              to={item.to}
              className={cn(
                'flex items-center gap-3 px-3 py-2 rounded-md text-sm font-medium transition-colors',
                isActive
                  ? 'bg-primary/10 text-primary'
                  : 'text-muted-foreground hover:bg-muted hover:text-foreground'
              )}
            >
              <Icon className="w-5 h-5 flex-shrink-0"/>
              {!collapsed && (
                <>
                  <span className="flex-1">{item.label}</span>
                  <kbd className="kbd">{item.shortcut}</kbd>
                </>
              )}
            </NavLink>
          );

          if (collapsed) {
            return (
              <Tooltip key={item.to} delayDuration={0}>
                <TooltipTrigger asChild>{linkContent}</TooltipTrigger>
                <TooltipContent side="right" className="flex items-center gap-2">
                  {item.label}
                  <kbd className="kbd">{item.shortcut}</kbd>
                </TooltipContent>
              </Tooltip>
            );
          }

          return linkContent;
        })}
      </nav>

      <Separator/>

      {/* Bottom section */}
      <div className="p-2 space-y-1">
        {(() => {
          const isActive = location.pathname === '/settings';

          const settingsContent = (
            <NavLink
              to="/settings"
              className={cn(
                'flex items-center gap-3 px-3 py-2 rounded-md text-sm font-medium transition-colors',
                isActive
                  ? 'bg-primary/10 text-primary'
                  : 'text-muted-foreground hover:bg-muted hover:text-foreground'
              )}
            >
              <Settings className="w-5 h-5 flex-shrink-0"/>
              {!collapsed && (
                <>
                  <span className="flex-1">Settings</span>
                  <kbd className="kbd">,</kbd>
                </>
              )}
            </NavLink>
          );

          if (collapsed) {
            return (
              <Tooltip delayDuration={0}>
                <TooltipTrigger asChild>{settingsContent}</TooltipTrigger>
                <TooltipContent side="right" className="flex items-center gap-2">
                  Settings
                  <kbd className="kbd">,</kbd>
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
          className="w-full justify-start"
          onClick={() => onCollapsedChange(!collapsed)}
        >
          {collapsed ? (
            <ChevronRight className="w-5 h-5"/>
          ) : (
            <>
              <ChevronLeft className="w-5 h-5"/>
              <span className="ml-3">Collapse</span>
            </>
          )}
        </Button>
      </div>
    </aside>
  );
}
