import { ACTIONS } from "../shared/actions.js";
import type {
  ActionRequest,
  ActionResponse,
} from "../background/service-worker.js";

const HOST_ID = "qyl-ai-toolbar-host";
const REPLACED_ATTR = "data-qyl-replaced";
const MAX_PAGE_CHARS = 4000;

let currentHost: HTMLElement | null = null;
let lastSelection: { text: string; range: Range } | null = null;

// ── Undo stack (toast-based, independent of DOM hover state) ──

interface UndoEntry {
  id: number;
  wrapper: HTMLElement;
  originalNodes: Node[];
  originalText: string;
}

let undoCounter = 0;
const undoStack: UndoEntry[] = [];
let undoToast: HTMLElement | null = null;

// ── Selection expansion ──

function expandToWordBoundaries(range: Range): Range {
  const expanded = range.cloneRange();

  // Expand start to word boundary
  const startContainer = expanded.startContainer;
  if (startContainer.nodeType === Node.TEXT_NODE) {
    const text = startContainer.textContent ?? "";
    let start = expanded.startOffset;
    while (start > 0 && /\S/.test(text[start - 1])) start--;
    expanded.setStart(startContainer, start);
  }

  // Expand end to word boundary
  const endContainer = expanded.endContainer;
  if (endContainer.nodeType === Node.TEXT_NODE) {
    const text = endContainer.textContent ?? "";
    let end = expanded.endOffset;
    while (end < text.length && /\S/.test(text[end])) end++;
    expanded.setEnd(endContainer, end);
  }

  return expanded;
}

function smartExpandSelection(range: Range): Range {
  const text = range.toString().trim();

  // Single partial word → expand to full word
  if (!text.includes(" ")) {
    return expandToWordBoundaries(range);
  }

  // Multi-word: check if start/end are mid-word, expand to word boundaries
  return expandToWordBoundaries(range);
}

// ── Toolbar ──

function createToolbar(
  text: string,
  range: Range,
  x: number,
  y: number,
): void {
  removeToolbar();
  lastSelection = { text, range: range.cloneRange() };

  const host = document.createElement("div");
  host.id = HOST_ID;
  host.style.cssText = `position:absolute;left:${x}px;top:${y}px;z-index:2147483647`;

  const shadow = host.attachShadow({ mode: "closed" });
  const style = document.createElement("style");
  style.textContent = getToolbarStyles();
  shadow.appendChild(style);

  const toolbar = document.createElement("div");
  toolbar.className = "qyl-toolbar";

  for (const action of ACTIONS) {
    const btn = document.createElement("button");
    btn.className = "qyl-action-btn";
    btn.title = action.label;
    btn.textContent = `${action.icon} ${action.label}`;
    btn.addEventListener("click", (e) => {
      e.stopPropagation();
      if (lastSelection) {
        const expanded = smartExpandSelection(lastSelection.range);
        handleAction(action.id, expanded.toString().trim(), expanded);
      }
    });
    toolbar.appendChild(btn);
  }

  shadow.appendChild(toolbar);
  document.body.appendChild(host);
  currentHost = host;
}

function removeToolbar(): void {
  currentHost?.remove();
  currentHost = null;
}

// ── Inline replacement ──

function handleAction(
  actionId: string,
  text: string,
  range: Range,
): void {
  const wrapper = document.createElement("span");
  wrapper.setAttribute(REPLACED_ATTR, "loading");

  // Save original nodes for undo
  const originalNodes: Node[] = [];
  const fragment = range.cloneContents();
  for (const child of Array.from(fragment.childNodes)) {
    originalNodes.push(child);
  }

  // Wrap selection — keeps text visible during loading
  try {
    range.surroundContents(wrapper);
  } catch {
    const extracted = range.extractContents();
    wrapper.appendChild(extracted);
    range.insertNode(wrapper);
  }

  // Loading style: dim + accent border
  applyStyle(wrapper, "loading");
  removeToolbar();

  const message: ActionRequest = { type: "action", actionId, text };

  chrome.runtime.sendMessage(message, (response: ActionResponse) => {
    if (response.type === "error") {
      restoreOriginal(wrapper, originalNodes);
      showToast(`Error: ${response.content}`);
    } else {
      wrapper.textContent = response.content;
      wrapper.setAttribute(REPLACED_ATTR, "done");
      applyStyle(wrapper, "done");

      const entry: UndoEntry = {
        id: ++undoCounter,
        wrapper,
        originalNodes,
        originalText: text,
      };
      undoStack.push(entry);
      showUndoToast();
    }
  });
}

function restoreOriginal(wrapper: HTMLElement, originalNodes: Node[]): void {
  const parent = wrapper.parentNode;
  if (!parent) return;
  for (const node of originalNodes) {
    parent.insertBefore(node, wrapper);
  }
  wrapper.remove();
}

// ── Replacement styling (dark/light aware) ──

function applyStyle(el: HTMLElement, state: "loading" | "done"): void {
  const isDark = window.matchMedia("(prefers-color-scheme: dark)").matches;

  if (state === "loading") {
    el.style.cssText = `
      opacity: 0.5;
      border-left: 3px solid ${isDark ? "#7986cb" : "#5c6bc0"};
      padding-left: 4px;
      transition: opacity 0.3s;
    `;
  } else {
    el.style.cssText = `
      background: ${isDark ? "rgba(121,134,203,0.15)" : "rgba(92,107,192,0.1)"};
      border-left: 3px solid ${isDark ? "#7986cb" : "#5c6bc0"};
      padding-left: 4px;
      border-radius: 2px;
      transition: background 0.3s;
    `;
  }
}

// ── Toast-based undo (Gmail pattern, always accessible) ──

function showUndoToast(): void {
  undoToast?.remove();

  const host = document.createElement("div");
  host.style.cssText = `
    position: fixed;
    bottom: 20px;
    left: 50%;
    transform: translateX(-50%);
    z-index: 2147483647;
  `;

  const shadow = host.attachShadow({ mode: "closed" });
  const isDark = window.matchMedia("(prefers-color-scheme: dark)").matches;

  shadow.innerHTML = `
    <style>
      .toast {
        display: flex;
        align-items: center;
        gap: 12px;
        padding: 10px 16px;
        background: ${isDark ? "#2a2a3e" : "#333"};
        color: ${isDark ? "#e0e0e0" : "#fff"};
        border-radius: 8px;
        font: 13px -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
        box-shadow: 0 4px 24px rgba(0,0,0,0.4);
        animation: slideUp 0.2s ease-out;
      }
      .count { opacity: 0.7; }
      button {
        padding: 4px 12px;
        border: 1px solid ${isDark ? "#7986cb" : "#5c6bc0"};
        border-radius: 5px;
        background: transparent;
        color: ${isDark ? "#7986cb" : "#8c9eff"};
        font: 13px -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
        cursor: pointer;
        transition: background 0.15s;
      }
      button:hover {
        background: ${isDark ? "rgba(121,134,203,0.2)" : "rgba(140,158,255,0.2)"};
      }
      .undo-all { opacity: 0.6; border: none; text-decoration: underline; }
      .undo-all:hover { opacity: 1; background: transparent; }
      @keyframes slideUp {
        from { opacity: 0; transform: translateY(10px); }
        to { opacity: 1; transform: translateY(0); }
      }
    </style>
    <div class="toast">
      <span class="count">${undoStack.length} change${undoStack.length > 1 ? "s" : ""}</span>
      <button class="undo-last">Undo</button>
      ${undoStack.length > 1 ? '<button class="undo-all">Undo all</button>' : ""}
    </div>
  `;

  shadow.querySelector(".undo-last")!.addEventListener("click", (e) => {
    e.stopPropagation();
    undoLast();
  });

  shadow.querySelector(".undo-all")?.addEventListener("click", (e) => {
    e.stopPropagation();
    undoAll();
  });

  document.body.appendChild(host);
  undoToast = host;
}

function removeUndoToast(): void {
  undoToast?.remove();
  undoToast = null;
}

function undoLast(): void {
  const entry = undoStack.pop();
  if (!entry) {
    removeUndoToast();
    return;
  }

  restoreOriginal(entry.wrapper, entry.originalNodes);

  if (undoStack.length > 0) {
    showUndoToast();
  } else {
    removeUndoToast();
  }

  // Reopen toolbar on the restored text
  reopenToolbar(entry.originalText, entry.originalNodes);
}

function undoAll(): void {
  while (undoStack.length > 0) {
    const entry = undoStack.pop()!;
    restoreOriginal(entry.wrapper, entry.originalNodes);
  }
  removeUndoToast();
}

function reopenToolbar(text: string, nodes: Node[]): void {
  const firstNode = nodes[0];
  if (!firstNode?.parentNode) return;

  const tempRange = document.createRange();
  tempRange.selectNode(firstNode);
  if (nodes.length > 1) {
    tempRange.setEndAfter(nodes[nodes.length - 1]);
  }

  const selection = window.getSelection();
  selection?.removeAllRanges();
  selection?.addRange(tempRange);

  const rect = tempRange.getBoundingClientRect();
  createToolbar(
    text,
    tempRange,
    rect.left + window.scrollX,
    rect.bottom + window.scrollY + 8,
  );
}

// ── Error/info toast ──

function showToast(message: string): void {
  const host = document.createElement("div");
  host.style.cssText = `position:fixed;top:16px;right:16px;z-index:2147483647`;

  const shadow = host.attachShadow({ mode: "closed" });
  shadow.innerHTML = `
    <style>
      .t { padding:10px 14px; background:#3d0000; color:#ff8a8a; border-radius:8px;
        font:13px -apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif;
        box-shadow:0 4px 20px rgba(0,0,0,0.3); max-width:400px; }
    </style>
    <div class="t">${message.replace(/</g, "&lt;")}</div>
  `;

  document.body.appendChild(host);
  setTimeout(() => host.remove(), 4000);
}

// ── Event listeners ──

document.addEventListener("mouseup", (e) => {
  const target = e.target as HTMLElement;
  if (target?.closest?.(`#${HOST_ID}`)) return;

  const selected = window.getSelection()?.toString().trim() ?? "";
  if (selected.length < 2) {
    removeToolbar();
    return;
  }

  const selection = window.getSelection();
  if (!selection || selection.rangeCount === 0) return;

  const range = selection.getRangeAt(0);
  const rect = range.getBoundingClientRect();

  createToolbar(
    selected,
    range,
    rect.left + window.scrollX,
    rect.bottom + window.scrollY + 8,
  );
});

document.addEventListener("keydown", (e) => {
  if ((e.ctrlKey || e.metaKey) && e.shiftKey && e.key === "Q") {
    e.preventDefault();
    const sel = window.getSelection();
    const text = sel?.toString().trim() || getPageText();
    const range = sel?.rangeCount ? sel.getRangeAt(0) : document.createRange();
    createToolbar(text, range, window.innerWidth / 2 - 200, window.scrollY + 100);
  }
});

document.addEventListener("keydown", (e) => {
  // Ctrl/Cmd+Z to undo last replacement
  if ((e.ctrlKey || e.metaKey) && e.key === "z" && undoStack.length > 0) {
    e.preventDefault();
    undoLast();
  }
  if (e.key === "Escape") removeToolbar();
});

document.addEventListener("mousedown", (e) => {
  const target = e.target as HTMLElement;
  if (!target?.closest?.(`#${HOST_ID}`)) {
    removeToolbar();
  }
});

function getPageText(): string {
  const text = document.body.innerText.trim();
  return text.length > MAX_PAGE_CHARS
    ? text.slice(0, MAX_PAGE_CHARS) + "..."
    : text;
}

// ── Toolbar styles ──

function getToolbarStyles(): string {
  return `
    :host {
      all: initial;
      font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
    }
    .qyl-toolbar {
      display: flex;
      gap: 4px;
      padding: 6px;
      background: var(--bg);
      border: 1px solid var(--border);
      border-radius: 10px;
      box-shadow: 0 4px 20px rgba(0, 0, 0, 0.3);
      animation: fadeIn 0.15s ease-out;
      --bg: #1a1a2e;
      --border: #333;
      --text: #e0e0e0;
      --hover: rgba(255, 255, 255, 0.1);
    }
    .qyl-action-btn {
      display: flex;
      align-items: center;
      gap: 4px;
      padding: 6px 10px;
      border: none;
      border-radius: 6px;
      background: transparent;
      color: var(--text);
      font-size: 12px;
      cursor: pointer;
      white-space: nowrap;
      transition: background 0.15s;
    }
    .qyl-action-btn:hover { background: var(--hover); }
    .qyl-action-btn:active { background: rgba(255, 255, 255, 0.15); }
    @keyframes fadeIn {
      from { opacity: 0; transform: translateY(-4px); }
      to { opacity: 1; transform: translateY(0); }
    }
    @media (prefers-color-scheme: light) {
      .qyl-toolbar {
        --bg: #ffffff;
        --border: #e0e0e0;
        --text: #333;
        --hover: rgba(0, 0, 0, 0.06);
        box-shadow: 0 4px 20px rgba(0, 0, 0, 0.1);
      }
      .qyl-action-btn:active { background: rgba(0, 0, 0, 0.1); }
    }
  `;
}
