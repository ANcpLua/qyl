export interface Action {
  id: string;
  label: string;
  icon: string;
  systemPrompt: string;
  shortTextPrompt: string;
}

const OUTPUT_RULE =
  "Output ONLY the result. No preamble, no explanation, no 'Here is...'. Start directly with the content.";

export const ACTIONS: Action[] = [
  {
    id: "tldr",
    label: "TLDR",
    icon: "\u26A1",
    systemPrompt: `Summarize the following text in 2-3 concise bullet points using \u2022 markers. Keep only the essential information. ${OUTPUT_RULE}`,
    shortTextPrompt: `Give a one-sentence summary of this phrase or concept. ${OUTPUT_RULE}`,
  },
  {
    id: "remove-fluff",
    label: "Remove fluff",
    icon: "\u2702\uFE0F",
    systemPrompt: `Rewrite the following text removing all filler words, redundant phrases, and fluff while preserving the original meaning and key information. ${OUTPUT_RULE}`,
    shortTextPrompt: `Rewrite this phrase more concisely. If it's a single word, return a more precise synonym. ${OUTPUT_RULE}`,
  },
  {
    id: "simplify",
    label: "Simplify",
    icon: "\uD83D\uDCA1",
    systemPrompt: `Rewrite the following text at an 8th grade reading level. Use simple words, short sentences, and clear structure. ${OUTPUT_RULE}`,
    shortTextPrompt: `Rewrite this word or phrase in simpler language. If it's a single word, return a simpler synonym. ${OUTPUT_RULE}`,
  },
  {
    id: "challenge",
    label: "Challenge this",
    icon: "\uD83E\uDD14",
    systemPrompt: `List the main counterarguments, weaknesses, and logical gaps in the following text. Be specific and constructive. ${OUTPUT_RULE}`,
    shortTextPrompt: `Challenge this claim or concept. Give one concise counterpoint. ${OUTPUT_RULE}`,
  },
  {
    id: "examples",
    label: "Practical examples",
    icon: "\uD83D\uDCCB",
    systemPrompt: `Give 3 concrete, real-world examples that illustrate the concepts in the following text. Be specific and practical. ${OUTPUT_RULE}`,
    shortTextPrompt: `Give one concrete real-world example of this word or concept. ${OUTPUT_RULE}`,
  },
];
