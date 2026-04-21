import {Component, type ErrorInfo, type ReactNode} from 'react';

interface Props {
    children: ReactNode;
    fallback?: ReactNode;
}

interface State {
    error: Error | null;
}

const STALE_CHUNK_RETRY_KEY = 'qyl:stale-chunk-retry';
const STALE_CHUNK_RETRY_WINDOW_MS = 60_000;

function isRecoverableChunkLoadError(error: Error): boolean {
    const message = error.message;

    return message.includes('Failed to fetch dynamically imported module')
        || message.includes('Importing a module script failed')
        || message.includes('ChunkLoadError')
        || message.includes('Loading chunk');
}

function getRecoverySignature(error: Error): string {
    const chunkUrl = error.message.match(/https?:\/\/\S+\.js\b/)?.[0]
        ?? error.message.match(/\/assets\/\S+\.js\b/)?.[0]
        ?? error.message;

    return `${window.location.pathname}|${chunkUrl}`;
}

function shouldReloadForChunkError(error: Error): boolean {
    if (!isRecoverableChunkLoadError(error)) {
        return false;
    }

    try {
        const raw = sessionStorage.getItem(STALE_CHUNK_RETRY_KEY);
        const previous = raw ? JSON.parse(raw) as { signature?: string; at?: number } : null;
        const signature = getRecoverySignature(error);
        const now = Date.now();

        if (
            previous?.signature === signature
            && typeof previous.at === 'number'
            && now - previous.at < STALE_CHUNK_RETRY_WINDOW_MS
        ) {
            return false;
        }

        sessionStorage.setItem(
            STALE_CHUNK_RETRY_KEY,
            JSON.stringify({signature, at: now}),
        );

        return true;
    } catch {
        return false;
    }
}

export class ErrorBoundary extends Component<Props, State> {
    state: State = {error: null};

    static getDerivedStateFromError(error: Error): State {
        return {error};
    }

    componentDidCatch(error: Error, info: ErrorInfo) {
        console.error('[ErrorBoundary]', error, info.componentStack);

        if (shouldReloadForChunkError(error)) {
            window.location.reload();
        }
    }

    render() {
        if (this.state.error) {
            if (this.props.fallback) return this.props.fallback;
            const isChunkError = isRecoverableChunkLoadError(this.state.error);
            return (
                <div className="flex-1 flex items-center justify-center p-8">
                    <div className="text-center space-y-4 max-w-md">
                        <h2 className="text-lg font-bold text-brutal-white">Something went wrong</h2>
                        <p className="text-sm text-brutal-slate">{this.state.error.message}</p>
                        <button
                            onClick={() => {
                                if (isChunkError) {
                                    window.location.reload();
                                    return;
                                }

                                this.setState({error: null});
                            }}
                            className="px-4 py-2 text-sm font-bold text-brutal-white bg-signal-orange/20 border border-signal-orange/40 hover:bg-signal-orange/30 transition-colors focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-signal-orange"
                        >
                            {isChunkError ? 'Reload page' : 'Try again'}
                        </button>
                    </div>
                </div>
            );
        }
        return this.props.children;
    }
}
