import {
  type AiConfig,
  type AiProvider,
  defaultEndpointFor,
  defaultModelFor,
  loadConfig,
  saveConfig,
} from "../shared/config.js";

const providerEl = document.getElementById("provider") as HTMLSelectElement;
const apiKeyEl = document.getElementById("apiKey") as HTMLInputElement;
const modelEl = document.getElementById("model") as HTMLSelectElement;
const endpointEl = document.getElementById("endpoint") as HTMLInputElement;
const endpointRowEl = document.getElementById("endpointRow") as HTMLElement;
const statusEl = document.getElementById("status") as HTMLElement;
const saveBtn = document.getElementById("save") as HTMLButtonElement;
const savedEl = document.getElementById("saved") as HTMLElement;

async function fetchOllamaModels(endpoint: string): Promise<string[]> {
  try {
    const res = await fetch(`${endpoint}/api/tags`);
    if (!res.ok) return [];
    const data = await res.json();
    return (data.models ?? []).map((m: { name: string }) => m.name);
  } catch {
    return [];
  }
}

function populateModelDropdown(models: string[], selected: string): void {
  modelEl.innerHTML = "";

  if (models.length === 0) {
    const opt = document.createElement("option");
    opt.value = selected || defaultModelFor(providerEl.value as AiProvider);
    opt.textContent = opt.value;
    modelEl.appendChild(opt);
    return;
  }

  for (const model of models) {
    const opt = document.createElement("option");
    opt.value = model;
    opt.textContent = model;
    modelEl.appendChild(opt);
  }

  if (selected && models.includes(selected)) {
    modelEl.value = selected;
  } else {
    modelEl.value = models[0];
  }
}

async function loadModelsForProvider(
  provider: AiProvider,
  endpoint: string,
  selected: string,
): Promise<void> {
  if (provider === "ollama") {
    modelEl.innerHTML = '<option value="">Loading...</option>';
    const models = await fetchOllamaModels(endpoint);
    if (models.length === 0) {
      populateModelDropdown([], selected || "llama3.2:1b");
      modelEl.innerHTML =
        '<option value="">No models found — run: ollama pull llama3.2:1b</option>';
    } else {
      populateModelDropdown(models, selected);
    }
  } else {
    const defaults: Record<string, string[]> = {
      openai: ["gpt-4o-mini", "gpt-4o", "gpt-4.1-mini", "gpt-4.1-nano"],
      anthropic: [
        "claude-haiku-4-5-20251001",
        "claude-sonnet-4-5-20250514",
        "claude-opus-4-6-20250612",
      ],
    };
    populateModelDropdown(
      defaults[provider] ?? [],
      selected || defaultModelFor(provider),
    );
  }
}

function updateUI(config: AiConfig): void {
  providerEl.value = config.provider;
  apiKeyEl.value = config.apiKey;
  endpointEl.value = config.endpoint;

  const isOllama = config.provider === "ollama";
  endpointRowEl.classList.toggle("visible", isOllama);
  apiKeyEl.placeholder = isOllama ? "(not required)" : "sk-...";

  const isConfigured = config.apiKey || isOllama;
  statusEl.textContent = isConfigured ? "Configured" : "Not configured";
  statusEl.className = `status ${isConfigured ? "configured" : "not-configured"}`;

  loadModelsForProvider(config.provider, config.endpoint, config.model);
}

providerEl.addEventListener("change", () => {
  const provider = providerEl.value as AiProvider;
  const endpoint = defaultEndpointFor(provider);
  endpointEl.value = endpoint;

  const isOllama = provider === "ollama";
  endpointRowEl.classList.toggle("visible", isOllama);
  apiKeyEl.placeholder = isOllama ? "(not required)" : "sk-...";

  loadModelsForProvider(provider, endpoint, "");
});

saveBtn.addEventListener("click", async () => {
  const config: AiConfig = {
    provider: providerEl.value as AiProvider,
    apiKey: apiKeyEl.value.trim(),
    model: modelEl.value || defaultModelFor(providerEl.value as AiProvider),
    endpoint:
      endpointEl.value.trim() ||
      defaultEndpointFor(providerEl.value as AiProvider),
  };

  await saveConfig(config);
  updateUI(config);

  savedEl.classList.add("show");
  setTimeout(() => savedEl.classList.remove("show"), 2000);
});

loadConfig().then(updateUI);
