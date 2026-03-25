import {Component, type ErrorInfo, type ReactNode} from 'react';

interface Props {
    children: ReactNode;
    fallback?: ReactNode;
}

interface State {
    error: Error | null;
}

export class ErrorBoundary extends Component<Props, State> {
    state: State = {error: null};

    static getDerivedStateFromError(error: Error): State {
        return {error};
    }

    componentDidCatch(error: Error, info: ErrorInfo) {
        console.error('[ErrorBoundary]', error, info.componentStack);
    }

    render() {
        if (this.state.error) {
            if (this.props.fallback) return this.props.fallback;
            return (
                <div className="flex-1 flex items-center justify-center p-8">
                    <div className="text-center space-y-4 max-w-md">
                        <h2 className="text-lg font-bold text-brutal-white">Something went wrong</h2>
                        <p className="text-sm text-brutal-slate">{this.state.error.message}</p>
                        <button
                            onClick={() => this.setState({error: null})}
                            className="px-4 py-2 text-sm font-bold text-brutal-white bg-signal-orange/20 border border-signal-orange/40 hover:bg-signal-orange/30 transition-colors focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-signal-orange"
                        >
                            Try again
                        </button>
                    </div>
                </div>
            );
        }
        return this.props.children;
    }
}
