import type { AiConfig } from "./config.js";

export async function complete(
  systemPrompt: string,
  text: string,
  config: AiConfig,
): Promise<string> {
  switch (config.provider) {
    case "openai":
      return completeOpenAI(systemPrompt, text, config);
    case "anthropic":
      return completeAnthropic(systemPrompt, text, config);
    case "ollama":
      return completeOllama(systemPrompt, text, config);
  }
}

async function completeOpenAI(
  systemPrompt: string,
  text: string,
  config: AiConfig,
): Promise<string> {
  const response = await fetch(
    `${config.endpoint}/v1/chat/completions`,
    {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        Authorization: `Bearer ${config.apiKey}`,
      },
      body: JSON.stringify({
        model: config.model,
        messages: [
          { role: "system", content: systemPrompt },
          { role: "user", content: text },
        ],
        max_tokens: 1024,
      }),
    },
  );

  if (!response.ok) {
    const err = await response.text();
    throw new Error(`OpenAI API error (${response.status}): ${err}`);
  }

  const data = await response.json();
  return data.choices[0].message.content;
}

async function completeAnthropic(
  systemPrompt: string,
  text: string,
  config: AiConfig,
): Promise<string> {
  const response = await fetch(`${config.endpoint}/v1/messages`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      "x-api-key": config.apiKey,
      "anthropic-version": "2023-06-01",
      "anthropic-dangerous-direct-browser-access": "true",
    },
    body: JSON.stringify({
      model: config.model,
      max_tokens: 1024,
      system: systemPrompt,
      messages: [{ role: "user", content: text }],
    }),
  });

  if (!response.ok) {
    const err = await response.text();
    throw new Error(`Anthropic API error (${response.status}): ${err}`);
  }

  const data = await response.json();
  return data.content[0].text;
}

async function completeOllama(
  systemPrompt: string,
  text: string,
  config: AiConfig,
): Promise<string> {
  const response = await fetch(`${config.endpoint}/api/generate`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({
      model: config.model,
      system: systemPrompt,
      prompt: text,
      stream: false,
    }),
  });

  if (!response.ok) {
    const err = await response.text();
    throw new Error(`Ollama API error (${response.status}): ${err}`);
  }

  const data = await response.json();
  return data.response;
}
