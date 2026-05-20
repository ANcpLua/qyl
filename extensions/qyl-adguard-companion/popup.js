const output = document.getElementById('output');
const ruleOutput = document.getElementById('ruleOutput');
const statusEl = document.getElementById('status');
const statusText = document.getElementById('statusText');

document.getElementById('helloButton').addEventListener('click', () => runNative('hello'));
document.getElementById('dnsButton').addEventListener('click', () => runNative('dns.status'));
document.getElementById('doctorButton').addEventListener('click', () => runNative('doctor'));
document.getElementById('snapshotButton').addEventListener('click', snapshotPage);
document.getElementById('ruleButton').addEventListener('click', suggestRule);
document.getElementById('copyRuleButton').addEventListener('click', copyRule);
document.getElementById('statsRefresh').addEventListener('click', refreshStats);

void boot();

async function boot() {
  try {
    const result = await native('hello', {});
    setStatus(true, `${result.hostName} ready`);
    show(result);
    void refreshStats();
  } catch (error) {
    setStatus(false, error.message);
    show({error: error.message});
  }
}

async function refreshStats() {
  try {
    const stats = await native('get_stats', {});
    document.getElementById('statUptime').textContent = formatUptime(stats.uptimeSeconds);
    document.getElementById('statRequests').textContent = String(stats.requestsTotal ?? 0);
    document.getElementById('statBatches').textContent = String(stats.networkBatches ?? 0);
    document.getElementById('statEvents').textContent = String(stats.networkEvents ?? 0);
    document.getElementById('statBlocked').textContent = String(stats.blockedByClient ?? 0);
    document.getElementById('statErrors').textContent = String(stats.requestsFailed ?? 0);
  } catch (error) {
    // Stats are advisory — don't surface to user-visible status.
    console.warn('qyl-companion get_stats failed:', error.message);
  }
}

function formatUptime(seconds) {
  const total = Number(seconds ?? 0);
  if (!Number.isFinite(total) || total <= 0) {
    return '0s';
  }
  const days = Math.floor(total / 86400);
  const hours = Math.floor((total % 86400) / 3600);
  const minutes = Math.floor((total % 3600) / 60);
  const secs = Math.floor(total % 60);
  if (days > 0) {
    return `${days}d ${hours}h`;
  }
  if (hours > 0) {
    return `${hours}h ${minutes}m`;
  }
  if (minutes > 0) {
    return `${minutes}m ${secs}s`;
  }
  return `${secs}s`;
}

async function runNative(method, params = {}) {
  try {
    const result = await native(method, params);
    setStatus(true, `${method} ok`);
    show(result);
  } catch (error) {
    setStatus(false, error.message);
    show({error: error.message});
  }
}

async function snapshotPage() {
  try {
    const result = await chrome.runtime.sendMessage({type: 'popup-snapshot'});
    if (!result.ok) {
      throw new Error(result.error);
    }
    setStatus(true, 'page summarized');
    show(result.result);
  } catch (error) {
    setStatus(false, error.message);
    show({error: error.message});
  }
}

async function suggestRule() {
  try {
    const [tab] = await chrome.tabs.query({active: true, currentWindow: true});
    const result = await native('rule.suggest', {
      pageUrl: tab?.url,
      targetUrl: tab?.url,
      reason: 'popup',
    });
    const firstRule = result.suggestions?.find(item => item.rule)?.rule ?? '';
    ruleOutput.value = firstRule;
    setStatus(true, 'rule suggestion ready');
    show(result);
  } catch (error) {
    setStatus(false, error.message);
    show({error: error.message});
  }
}

async function copyRule() {
  if (!ruleOutput.value) {
    return;
  }
  await navigator.clipboard.writeText(ruleOutput.value);
  setStatus(true, 'rule copied');
}

async function native(method, params) {
  const response = await chrome.runtime.sendMessage({type: 'native', method, params});
  if (!response.ok) {
    throw new Error(response.error);
  }
  return response.result;
}

function show(value) {
  output.textContent = JSON.stringify(value, null, 2);
}

function setStatus(ok, text) {
  statusEl.classList.toggle('ok', ok);
  statusEl.classList.toggle('fail', !ok);
  statusText.textContent = text;
}
