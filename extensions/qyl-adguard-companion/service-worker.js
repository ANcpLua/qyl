const HOST_NAME = 'dev.qyl.adguard_companion';
const SCHEMA_VERSION = 1;
const MAX_BATCH_SIZE = 30;
const FLUSH_INTERVAL_MS = 3000;

let port = null;
let pending = new Map();
let networkBuffer = [];
let networkTimer = null;

chrome.runtime.onInstalled.addListener(() => {
  chrome.contextMenus.create({
    id: 'qyl-inspect-page',
    title: 'Inspect page with qyl AdGuard Companion',
    contexts: ['page'],
  });
  chrome.contextMenus.create({
    id: 'qyl-suggest-rule',
    title: 'Suggest AdGuard rule for this element',
    contexts: ['selection', 'image', 'video', 'audio', 'link'],
  });
});

chrome.contextMenus.onClicked.addListener(async (info, tab) => {
  if (!tab?.id || !tab.url) {
    return;
  }

  if (info.menuItemId === 'qyl-inspect-page') {
    await snapshotTab(tab);
    return;
  }

  if (info.menuItemId === 'qyl-suggest-rule') {
    const targetUrl = info.srcUrl ?? info.linkUrl ?? tab.url;
    const result = await requestNative('rule.suggest', {
      pageUrl: tab.url,
      targetUrl,
      reason: 'context-menu',
    });
    await chrome.storage.session.set({lastRuleSuggestion: result});
  }
});

chrome.runtime.onMessage.addListener((message, sender, sendResponse) => {
  handleMessage(message, sender)
    .then(result => sendResponse({ok: true, result}))
    .catch(error => sendResponse({ok: false, error: String(error?.message ?? error)}));
  return true;
});

chrome.webRequest.onErrorOccurred.addListener(
  details => {
    if (details.tabId < 0) {
      return;
    }

    networkBuffer.push({
      url: details.url,
      type: details.type,
      statusCode: 0,
      error: details.error,
      tabId: details.tabId,
    });

    if (networkBuffer.length >= MAX_BATCH_SIZE) {
      void flushNetworkBuffer();
    } else if (networkTimer === null) {
      networkTimer = setTimeout(() => {
        networkTimer = null;
        void flushNetworkBuffer();
      }, FLUSH_INTERVAL_MS);
    }
  },
  {urls: ['<all_urls>']}
);

async function handleMessage(message, sender) {
  switch (message?.type) {
    case 'native':
      return requestNative(message.method, message.params ?? {});
    case 'content-snapshot':
      await chrome.storage.session.set({
        lastPageSnapshot: {
          tabId: sender.tab?.id,
          url: sender.tab?.url,
          observedAt: Date.now(),
          result: await requestNative('page.snapshot', message.params ?? {}),
        },
      });
      return {stored: true};
    case 'popup-snapshot':
      return snapshotActiveTab();
    case 'flush-network':
      return flushNetworkBuffer();
    default:
      throw new Error(`Unknown message type: ${message?.type}`);
  }
}

async function snapshotActiveTab() {
  const [tab] = await chrome.tabs.query({active: true, currentWindow: true});
  if (!tab) {
    throw new Error('No active tab.');
  }

  return snapshotTab(tab);
}

async function snapshotTab(tab) {
  const params = {
    url: tab.url,
    title: tab.title,
  };

  const result = await requestNative('page.snapshot', params);
  await chrome.storage.session.set({lastPageSnapshot: {tabId: tab.id, url: tab.url, observedAt: Date.now(), result}});
  return result;
}

async function flushNetworkBuffer() {
  if (networkBuffer.length === 0) {
    return {received: 0, blockedByClient: 0, hosts: [], message: 'No buffered browser network errors.'};
  }

  const events = networkBuffer.splice(0, networkBuffer.length);
  const result = await requestNative('network.batch', {events});
  await chrome.storage.session.set({lastNetworkBatch: {...result, observedAt: Date.now()}});
  return result;
}

function connectHost() {
  if (port) {
    return port;
  }

  port = chrome.runtime.connectNative(HOST_NAME);
  port.onMessage.addListener(message => {
    const entry = pending.get(message.id);
    if (!entry) {
      return;
    }

    pending.delete(message.id);
    if (message.ok) {
      entry.resolve(message.result);
    } else {
      entry.reject(new Error(message.error?.message ?? 'Native host error'));
    }
  });
  port.onDisconnect.addListener(() => {
    const error = chrome.runtime.lastError?.message ?? 'Native host disconnected';
    for (const entry of pending.values()) {
      entry.reject(new Error(error));
    }
    pending.clear();
    port = null;
  });
  return port;
}

function requestNative(method, params) {
  const id = crypto.randomUUID();
  const nativePort = connectHost();
  return new Promise((resolve, reject) => {
    pending.set(id, {resolve, reject});
    nativePort.postMessage({id, schemaVersion: SCHEMA_VERSION, method, params});
  });
}
