export type AiProvider = "openai" | "anthropic" | "ollama";

export interface AiConfig {
  provider: AiProvider;
  apiKey: string;
  model: string;
  endpoint: string;
}

const DEFAULT_MODELS: Record<AiProvider, string> = {
  openai: "gpt-4o-mini",
  anthropic: "claude-haiku-4-5-20251001",
  ollama: "llama3.2:1b",
};

const DEFAULT_ENDPOINTS: Record<AiProvider, string> = {
  openai: "https://api.openai.com",
  anthropic: "https://api.anthropic.com",
  ollama: "http://localhost:11434",
};

export function defaultConfig(): AiConfig {
  return {
    provider: "openai",
    apiKey: "",
    model: DEFAULT_MODELS.openai,
    endpoint: DEFAULT_ENDPOINTS.openai,
  };
}

export function defaultModelFor(provider: AiProvider): string {
  return DEFAULT_MODELS[provider];
}

export function defaultEndpointFor(provider: AiProvider): string {
  return DEFAULT_ENDPOINTS[provider];
}

export async function loadConfig(): Promise<AiConfig> {
  const result = await chrome.storage.sync.get("qylAiConfig");
  const stored = result.qylAiConfig as AiConfig | undefined;
  return stored ?? defaultConfig();
}

export async function saveConfig(config: AiConfig): Promise<void> {
  await chrome.storage.sync.set({ qylAiConfig: config });
}
