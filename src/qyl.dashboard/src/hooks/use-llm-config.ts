import {useCallback, useState} from 'react';

const STORAGE_KEY = 'qyl-llm-config';

export type LlmProvider = 'openai' | 'anthropic' | 'ollama' | 'openai-compatible';

export interface LlmConfig {
    provider: LlmProvider;
    apiKey?: string;
    model?: string;
    endpoint?: string;
}

function readConfig(): LlmConfig | null {
    if (typeof window === 'undefined') return null;
    try {
        const raw = localStorage.getItem(STORAGE_KEY);
        if (!raw) return null;
        const parsed = JSON.parse(raw) as LlmConfig;
        return parsed.provider ? parsed : null;
    } catch {
        return null;
    }
}

export function useLlmConfig() {
    const [config, setConfigState] = useState<LlmConfig | null>(readConfig);

    const setConfig = useCallback((next: LlmConfig | null) => {
        setConfigState(next);
        if (next) {
            localStorage.setItem(STORAGE_KEY, JSON.stringify(next));
        } else {
            localStorage.removeItem(STORAGE_KEY);
        }
    }, []);

    return {
        config,
        setConfig,
        isConfigured: config !== null && config.provider.length > 0,
    };
}

/** Provider display info for UI dropdowns */
export const LLM_PROVIDERS: ReadonlyArray<{
    value: LlmProvider;
    label: string;
    needsKey: boolean;
    defaultModel: string;
    defaultEndpoint?: string;
}> = [
    {value: 'openai', label: 'OpenAI', needsKey: true, defaultModel: 'gpt-4o-mini'},
    {value: 'anthropic', label: 'Anthropic', needsKey: true, defaultModel: 'claude-sonnet-4-6'},
    {value: 'ollama', label: 'Ollama', needsKey: false, defaultModel: 'llama3', defaultEndpoint: 'http://localhost:11434'},
    {value: 'openai-compatible', label: 'OpenAI-compatible', needsKey: false, defaultModel: ''},
];
