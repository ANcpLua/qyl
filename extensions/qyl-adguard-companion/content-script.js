(() => {
  const blockedPlaceholders = document.querySelectorAll(
    '[class*="adguard" i], [id*="adguard" i], [class*="blocked" i], [id*="blocked" i]'
  ).length;

  const params = {
    url: location.href,
    title: document.title,
    scriptCount: document.scripts.length,
    imageCount: document.images.length,
    iframeCount: document.querySelectorAll('iframe').length,
    blockedPlaceholders,
    adGuardDetected: blockedPlaceholders > 0,
  };

  chrome.runtime.sendMessage({type: 'content-snapshot', params}).catch(() => {
    // Service-worker sleep or native-host absence should not affect page behavior.
  });
})();
