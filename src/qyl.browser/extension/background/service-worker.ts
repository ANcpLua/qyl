import { ACTIONS } from "../shared/actions.js";
import { complete } from "../shared/ai-client.js";
import { loadConfig } from "../shared/config.js";

export interface ActionRequest {
  type: "action";
  actionId: string;
  text: string;
}

export interface ActionResponse {
  type: "result" | "error";
  content: string;
}

chrome.runtime.onMessage.addListener(
  (message: ActionRequest, _sender, sendResponse) => {
    if (message.type !== "action") return false;

    const action = ACTIONS.find((a) => a.id === message.actionId);
    if (!action) {
      sendResponse({ type: "error", content: "Unknown action" } satisfies ActionResponse);
      return false;
    }

    const isShort = message.text.trim().split(/\s+/).length <= 5;
    const prompt = isShort ? action.shortTextPrompt : action.systemPrompt;

    handleAction(prompt, message.text)
      .then((content) =>
        sendResponse({ type: "result", content } satisfies ActionResponse),
      )
      .catch((err) =>
        sendResponse({
          type: "error",
          content: err instanceof Error ? err.message : String(err),
        } satisfies ActionResponse),
      );

    return true; // keep message channel open for async response
  },
);

async function handleAction(
  systemPrompt: string,
  text: string,
): Promise<string> {
  const config = await loadConfig();

  if (!config.apiKey && config.provider !== "ollama") {
    throw new Error(
      "No API key configured. Click the qyl extension icon to set up your AI provider.",
    );
  }

  return complete(systemPrompt, text, config);
}
