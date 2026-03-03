import {useLocation} from 'react-router-dom';

interface CopilotSuggestionsProps {
    onSelect: (question: string) => void;
}

const routeSuggestions: Record<string, string[]> = {
    '/': [
        'Summarize active resources',
        'What errors occurred recently?',
        'Which services are most active?',
    ],
    '/traces': [
        'Explain this trace',
        'Why is latency high?',
        'Find the slowest operations',
    ],
    '/logs': [
        'Summarize recent errors',
        'What caused the failures?',
        'Find recurring log patterns',
    ],
    '/genai': [
        'Analyze token usage',
        'Which model is most cost-effective?',
        'Compare provider latencies',
    ],
};

const defaultSuggestions = [
    'What happened in the last hour?',
    'Are there any anomalies?',
    'Summarize system health',
];

export function CopilotSuggestions({onSelect}: CopilotSuggestionsProps) {
    const location = useLocation();
    const suggestions = routeSuggestions[location.pathname] ?? defaultSuggestions;

    return (
        <div className="flex flex-col gap-2 p-2">
            <span className="text-[10px] font-bold tracking-[0.14em] text-brutal-slate uppercase">
                Suggested prompts
            </span>
            <div className="flex flex-wrap gap-2">
                {suggestions.map((q) => (
                    <button
                        key={q}
                        type="button"
                        onClick={() => onSelect(q)}
                        className="px-2.5 py-1.5 text-xs tracking-wide border border-brutal-zinc bg-brutal-dark/85 text-brutal-slate hover:border-signal-violet hover:text-brutal-white hover:bg-signal-violet/10 transition-colors"
                    >
                        {q}
                    </button>
                ))}
            </div>
        </div>
    );
}
