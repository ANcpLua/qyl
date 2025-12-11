import {useState} from 'react';
import {Database, Keyboard, LogOut, Monitor, Moon, Palette, Settings, Sun,} from 'lucide-react';
import {Card, CardContent, CardDescription, CardHeader, CardTitle} from '@/components/ui/card';
import {Button} from '@/components/ui/button';
import {Badge} from '@/components/ui/badge';
import {Input} from '@/components/ui/input';
import {Separator} from '@/components/ui/separator';
import {Tabs, TabsContent, TabsList, TabsTrigger} from '@/components/ui/tabs';
import {Select, SelectContent, SelectItem, SelectTrigger, SelectValue,} from '@/components/ui/select';

const keyboardShortcuts = [
  {key: 'G', description: 'Go to Resources'},
  {key: 'T', description: 'Go to Traces'},
  {key: 'L', description: 'Go to Logs'},
  {key: 'M', description: 'Go to Metrics'},
  {key: 'A', description: 'Go to GenAI'},
  {key: ',', description: 'Open Settings'},
  {key: '?', description: 'Show keyboard shortcuts'},
  {key: 'Ctrl + /', description: 'Focus search'},
  {key: 'Escape', description: 'Close panel / Clear selection'},
  {key: 'R', description: 'Refresh data'},
  {key: 'Space', description: 'Toggle live mode'},
];

export function SettingsPage() {
  const [theme, setTheme] = useState<'dark' | 'light' | 'system'>('dark');
  const [refreshInterval, setRefreshInterval] = useState('5');
  const [maxLogLines, setMaxLogLines] = useState('1000');

  const handleLogout = () => {
    fetch('/api/logout', {method: 'POST'})
      .then(() => window.location.reload());
  };

  return (
    <div className="p-6 max-w-4xl mx-auto space-y-6">
      <div>
        <h1 className="text-2xl font-bold">Settings</h1>
        <p className="text-muted-foreground">
          Configure your qyl. dashboard preferences
        </p>
      </div>

      <Tabs defaultValue="general" className="space-y-6">
        <TabsList className="grid w-full grid-cols-4">
          <TabsTrigger value="general">
            <Settings className="w-4 h-4 mr-2"/>
            General
          </TabsTrigger>
          <TabsTrigger value="appearance">
            <Palette className="w-4 h-4 mr-2"/>
            Appearance
          </TabsTrigger>
          <TabsTrigger value="shortcuts">
            <Keyboard className="w-4 h-4 mr-2"/>
            Shortcuts
          </TabsTrigger>
          <TabsTrigger value="data">
            <Database className="w-4 h-4 mr-2"/>
            Data
          </TabsTrigger>
        </TabsList>

        {/* General Settings */}
        <TabsContent value="general" className="space-y-4">
          <Card>
            <CardHeader>
              <CardTitle className="text-base">Session</CardTitle>
              <CardDescription>
                Manage your authentication session
              </CardDescription>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="flex items-center justify-between">
                <div>
                  <p className="font-medium">Logged In</p>
                  <p className="text-sm text-muted-foreground">
                    Session expires in 3 days
                  </p>
                </div>
                <Button variant="outline" onClick={handleLogout}>
                  <LogOut className="w-4 h-4 mr-2"/>
                  Logout
                </Button>
              </div>
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle className="text-base">Notifications</CardTitle>
              <CardDescription>
                Configure alert notifications
              </CardDescription>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="flex items-center justify-between">
                <div>
                  <p className="font-medium">Error Alerts</p>
                  <p className="text-sm text-muted-foreground">
                    Show notification for new errors
                  </p>
                </div>
                <Badge variant="secondary">Coming Soon</Badge>
              </div>
              <Separator/>
              <div className="flex items-center justify-between">
                <div>
                  <p className="font-medium">Cost Alerts</p>
                  <p className="text-sm text-muted-foreground">
                    Alert when GenAI cost exceeds threshold
                  </p>
                </div>
                <Badge variant="secondary">Coming Soon</Badge>
              </div>
            </CardContent>
          </Card>
        </TabsContent>

        {/* Appearance */}
        <TabsContent value="appearance" className="space-y-4">
          <Card>
            <CardHeader>
              <CardTitle className="text-base">Theme</CardTitle>
              <CardDescription>
                Choose your preferred color scheme
              </CardDescription>
            </CardHeader>
            <CardContent>
              <div className="grid grid-cols-3 gap-4">
                <Button
                  variant={theme === 'dark' ? 'default' : 'outline'}
                  className="justify-start"
                  onClick={() => setTheme('dark')}
                >
                  <Moon className="w-4 h-4 mr-2"/>
                  Dark
                </Button>
                <Button
                  variant={theme === 'light' ? 'default' : 'outline'}
                  className="justify-start"
                  onClick={() => setTheme('light')}
                >
                  <Sun className="w-4 h-4 mr-2"/>
                  Light
                </Button>
                <Button
                  variant={theme === 'system' ? 'default' : 'outline'}
                  className="justify-start"
                  onClick={() => setTheme('system')}
                >
                  <Monitor className="w-4 h-4 mr-2"/>
                  System
                </Button>
              </div>
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle className="text-base">Density</CardTitle>
              <CardDescription>
                Adjust the information density
              </CardDescription>
            </CardHeader>
            <CardContent>
              <Select defaultValue="comfortable">
                <SelectTrigger className="w-48">
                  <SelectValue/>
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="compact">Compact</SelectItem>
                  <SelectItem value="comfortable">Comfortable</SelectItem>
                  <SelectItem value="spacious">Spacious</SelectItem>
                </SelectContent>
              </Select>
            </CardContent>
          </Card>
        </TabsContent>

        {/* Keyboard Shortcuts */}
        <TabsContent value="shortcuts" className="space-y-4">
          <Card>
            <CardHeader>
              <CardTitle className="text-base">Keyboard Shortcuts</CardTitle>
              <CardDescription>
                Quick navigation and actions. Press <kbd className="kbd">?</kbd> anywhere to see this
                list.
              </CardDescription>
            </CardHeader>
            <CardContent>
              <div className="space-y-2">
                {keyboardShortcuts.map((shortcut) => (
                  <div
                    key={shortcut.key}
                    className="flex items-center justify-between py-2 border-b border-border last:border-0"
                  >
                    <span className="text-sm">{shortcut.description}</span>
                    <kbd className="kbd min-w-fit">{shortcut.key}</kbd>
                  </div>
                ))}
              </div>
            </CardContent>
          </Card>
        </TabsContent>

        {/* Data Settings */}
        <TabsContent value="data" className="space-y-4">
          <Card>
            <CardHeader>
              <CardTitle className="text-base">Auto-Refresh</CardTitle>
              <CardDescription>
                Configure automatic data refresh interval
              </CardDescription>
            </CardHeader>
            <CardContent>
              <div className="flex items-center gap-4">
                <Select value={refreshInterval} onValueChange={setRefreshInterval}>
                  <SelectTrigger className="w-48">
                    <SelectValue/>
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="1">Every 1 second</SelectItem>
                    <SelectItem value="5">Every 5 seconds</SelectItem>
                    <SelectItem value="10">Every 10 seconds</SelectItem>
                    <SelectItem value="30">Every 30 seconds</SelectItem>
                    <SelectItem value="60">Every 60 seconds</SelectItem>
                    <SelectItem value="0">Disabled</SelectItem>
                  </SelectContent>
                </Select>
              </div>
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle className="text-base">Log Buffer</CardTitle>
              <CardDescription>
                Maximum number of log lines to keep in memory
              </CardDescription>
            </CardHeader>
            <CardContent>
              <div className="flex items-center gap-4">
                <Input
                  type="number"
                  value={maxLogLines}
                  onChange={(e) => setMaxLogLines(e.target.value)}
                  className="w-32"
                />
                <span className="text-sm text-muted-foreground">lines</span>
              </div>
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle className="text-base">Storage</CardTitle>
              <CardDescription>
                DuckDB storage information
              </CardDescription>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="grid grid-cols-2 gap-4 text-sm">
                <div>
                  <span className="text-muted-foreground">Database Path:</span>
                  <code className="ml-2 font-mono text-primary">/data/qyl.duckdb</code>
                </div>
                <div>
                  <span className="text-muted-foreground">Database Size:</span>
                  <span className="ml-2 font-mono">--</span>
                </div>
              </div>
              <Separator/>
              <div className="flex gap-2">
                <Button variant="outline" size="sm" disabled>
                  Export Data
                </Button>
                <Button variant="destructive" size="sm" disabled>
                  Clear All Data
                </Button>
              </div>
              <p className="text-xs text-muted-foreground">
                Data management features coming soon
              </p>
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>

      {/* Version info */}
      <Card>
        <CardContent className="py-4">
          <div className="flex items-center justify-between text-sm">
            <div className="flex items-center gap-2">
              <span className="text-2xl font-bold text-gradient">qyl.</span>
              <span className="text-muted-foreground">AI Observability Dashboard</span>
            </div>
            <div className="text-muted-foreground">
              v0.1.0 | Built with DuckDB + SSE
            </div>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
