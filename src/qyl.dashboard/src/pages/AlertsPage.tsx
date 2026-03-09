import {Bell, Plus} from 'lucide-react';
import {Button} from '@/components/ui/button';
import {Card, CardContent} from '@/components/ui/card';
import {Tabs, TabsContent, TabsList, TabsTrigger} from '@/components/ui/tabs';
import {Tooltip, TooltipContent, TooltipTrigger} from '@/components/ui/tooltip';

function EmptyState() {
    return (
        <Card>
            <CardContent className="py-20 text-center">
                <Bell className="w-12 h-12 mx-auto mb-4 text-signal-orange"/>
                <h2 className="text-lg font-bold text-brutal-white mb-2">
                    More signal, less noise
                </h2>
                <p className="text-sm text-brutal-slate max-w-md mx-auto mb-6">
                    Set your own rules for alerts you need, with information that helps.
                </p>
                <div className="flex items-center justify-center gap-3">
                    <Button variant="outline">View Docs</Button>
                    <Tooltip>
                        <TooltipTrigger asChild>
                            <span tabIndex={0}>
                                <Button disabled>
                                    <Plus className="w-4 h-4 mr-2"/>
                                    Create Alert
                                </Button>
                            </span>
                        </TooltipTrigger>
                        <TooltipContent>Coming soon</TooltipContent>
                    </Tooltip>
                </div>
            </CardContent>
        </Card>
    );
}

export function AlertsPage() {
    return (
        <div className="p-6 space-y-6">
            <div className="flex items-center justify-between">
                <h1 className="text-2xl font-bold text-brutal-white">Alerts</h1>
                <Tooltip>
                    <TooltipTrigger asChild>
                        <span tabIndex={0}>
                            <Button disabled>
                                <Plus className="w-4 h-4 mr-2"/>
                                Create Alert
                            </Button>
                        </span>
                    </TooltipTrigger>
                    <TooltipContent>Coming soon</TooltipContent>
                </Tooltip>
            </div>

            <Tabs defaultValue="rules">
                <TabsList>
                    <TabsTrigger value="rules">Alert Rules</TabsTrigger>
                    <TabsTrigger value="history">History</TabsTrigger>
                </TabsList>

                <TabsContent value="rules">
                    <EmptyState/>
                </TabsContent>

                <TabsContent value="history">
                    <EmptyState/>
                </TabsContent>
            </Tabs>
        </div>
    );
}
