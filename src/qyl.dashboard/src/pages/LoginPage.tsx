import * as React from 'react';
import {useSearchParams} from 'react-router-dom';
import {ArrowRight, Eye, EyeOff, KeyRound, Terminal} from 'lucide-react';
import {Button} from '@/components/ui/button';
import {Input} from '@/components/ui/input';
import {Card} from '@/components/ui/card';
import {useLogin} from '@/hooks/use-auth';

export function LoginPage() {
    const [searchParams] = useSearchParams();
    const [token, setToken] = React.useState(searchParams.get('t') || '');
    const [showToken, setShowToken] = React.useState(false);
    const loginMutation = useLogin();
    const hasAttemptedAutoLogin = React.useRef(false);

    // Auto-login if token in URL (only once)
    React.useEffect(() => {
        const urlToken = searchParams.get('t');
        if (urlToken && !hasAttemptedAutoLogin.current) {
            hasAttemptedAutoLogin.current = true;
            loginMutation.mutate(urlToken);
        }
    }, [searchParams]);

    const handleSubmit = (e: React.FormEvent) => {
        e.preventDefault();
        if (token.trim()) {
            loginMutation.mutate(token.trim());
        }
    };

    return (
        <div className="min-h-screen flex items-center justify-center bg-background p-4">
            <Card className="w-full max-w-md p-8 space-y-6">
                {/* Logo */}
                <div className="flex flex-col items-center gap-2">
                    <div className="h-12 w-12 rounded-xl bg-primary/10 flex items-center justify-center">
                        <KeyRound className="h-6 w-6 text-primary"/>
                    </div>
                    <h1 className="text-2xl font-bold">qyl Dashboard</h1>
                    <p className="text-muted-foreground text-sm">
                        Enter your authentication token to continue
                    </p>
                </div>

                {/* Login Form */}
                <form onSubmit={handleSubmit} className="space-y-4">
                    <div className="relative">
                        <label htmlFor="token" className="sr-only">Authentication token</label>
                        <Input
                            id="token"
                            type={showToken ? 'text' : 'password'}
                            placeholder="Paste token here..."
                            value={token}
                            onChange={(e) => setToken(e.target.value)}
                            className="pr-10 font-mono"
                            autoFocus
                            autoComplete="off"
                            spellCheck={false}
                        />
                        <Button
                            type="button"
                            variant="ghost"
                            size="icon"
                            className="absolute right-0 top-0 h-full px-3"
                            onClick={() => setShowToken(!showToken)}
                            aria-label={showToken ? 'Hide token' : 'Show token'}
                        >
                            {showToken ? (
                                <EyeOff className="h-4 w-4 text-muted-foreground"/>
                            ) : (
                                <Eye className="h-4 w-4 text-muted-foreground"/>
                            )}
                        </Button>
                    </div>

                    <Button
                        type="submit"
                        className="w-full"
                        disabled={!token.trim() || loginMutation.isPending}
                    >
                        {loginMutation.isPending ? (
                            'Logging in...'
                        ) : (
                            <>
                                Log in
                                <ArrowRight className="ml-2 h-4 w-4"/>
                            </>
                        )}
                    </Button>
                </form>

                {/* Instructions */}
                <div className="rounded-lg bg-muted/50 p-4 space-y-3">
                    <div className="flex items-center gap-2 text-sm font-medium">
                        <Terminal className="h-4 w-4"/>
                        Where to find your token
                    </div>
                    <div className="text-xs text-muted-foreground space-y-2">
                        <p>Look for this line in your terminal when starting the collector:</p>
                        <code className="block bg-background rounded px-2 py-1 font-mono text-xs">
                            Login Token: <span className="text-primary">YOUR_TOKEN</span>
                        </code>
                        <p>Copy the token value and paste it above, or click the login link directly.</p>
                    </div>
                </div>

                {/* Footer */}
                <p className="text-center text-xs text-muted-foreground">
                    A new token is generated each time the collector starts.
                </p>
            </Card>
        </div>
    );
}
